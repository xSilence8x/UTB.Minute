using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace UTB.Minute.WebApi;

public sealed class KeycloakAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    IOptionsMonitor<KeycloakAuthenticationOptions> keycloakOptions,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private static readonly SemaphoreSlim metadataLock = new(1, 1);
    private static OpenIdConfiguration? cachedConfiguration;
    private static DateTimeOffset cachedConfigurationUntil;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.Authorization.ToString() is { } authorization &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return await AuthenticateBearerAsync(authorization["Bearer ".Length..].Trim());
        }

        if (ShouldAllowDebugRoleHeader())
        {
            return AuthenticateDebugRoleHeader();
        }

        return AuthenticateResult.NoResult();
    }

    private async Task<AuthenticateResult> AuthenticateBearerAsync(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return AuthenticateResult.Fail("Bearer token is not a JWT.");
        }

        try
        {
            using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var kid = header.RootElement.GetProperty("kid").GetString();
            var algorithm = header.RootElement.GetProperty("alg").GetString();
            if (algorithm != "RS256" || string.IsNullOrWhiteSpace(kid))
            {
                return AuthenticateResult.Fail("Only RS256 Keycloak tokens are supported.");
            }

            var openIdConfiguration = await GetOpenIdConfigurationAsync();
            if (!VerifySignature(parts[0], parts[1], parts[2], kid, openIdConfiguration))
            {
                return AuthenticateResult.Fail("JWT signature is invalid.");
            }

            var issuer = payload.RootElement.GetProperty("iss").GetString();
            if (!string.Equals(issuer, openIdConfiguration.Issuer, StringComparison.Ordinal))
            {
                return AuthenticateResult.Fail("JWT issuer is invalid.");
            }

            if (!IsTokenInLifetime(payload.RootElement))
            {
                return AuthenticateResult.Fail("JWT is expired or not valid yet.");
            }

            if (!HasAudience(payload.RootElement))
            {
                return AuthenticateResult.Fail("JWT audience is invalid.");
            }

            var claims = CreateClaims(payload.RootElement);
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail(ex);
        }
    }

    private bool ShouldAllowDebugRoleHeader() =>
        keycloakOptions.CurrentValue.AllowDebugRoleHeader ||
        environment.IsDevelopment() ||
        string.IsNullOrWhiteSpace(GetAuthority());

    private AuthenticateResult AuthenticateDebugRoleHeader()
    {
        if (!Request.Headers.TryGetValue(AuthConstants.RoleHeader, out var roleHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var roles = roleHeader.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (roles.Length == 0)
        {
            return AuthenticateResult.Fail("No role was provided.");
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, "development-user") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(claims, Scheme.Name);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private async Task<OpenIdConfiguration> GetOpenIdConfigurationAsync()
    {
        if (cachedConfiguration is not null && cachedConfigurationUntil > DateTimeOffset.UtcNow)
        {
            return cachedConfiguration;
        }

        await metadataLock.WaitAsync();
        try
        {
            if (cachedConfiguration is not null && cachedConfigurationUntil > DateTimeOffset.UtcNow)
            {
                return cachedConfiguration;
            }

            var authority = GetAuthority();
            if (string.IsNullOrWhiteSpace(authority))
            {
                throw new InvalidOperationException("Keycloak authority is not configured.");
            }

            var client = httpClientFactory.CreateClient();
            var metadata = await client.GetFromJsonAsync<JsonElement>($"{authority.TrimEnd('/')}/.well-known/openid-configuration");
            var issuer = metadata.GetProperty("issuer").GetString()!;
            var jwksUri = metadata.GetProperty("jwks_uri").GetString()!;
            var jwks = await client.GetFromJsonAsync<JsonElement>(jwksUri);
            cachedConfiguration = new OpenIdConfiguration(issuer, jwks.GetProperty("keys").Clone());
            cachedConfigurationUntil = DateTimeOffset.UtcNow.AddMinutes(10);

            return cachedConfiguration;
        }
        finally
        {
            metadataLock.Release();
        }
    }

    private string? GetAuthority()
    {
        var configured = keycloakOptions.CurrentValue.Authority;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var serviceEndpoint = configuration["services:keycloak:http:0"] ?? configuration["Services:keycloak:http:0"];
        return string.IsNullOrWhiteSpace(serviceEndpoint) ? null : $"{serviceEndpoint.TrimEnd('/')}/realms/minute";
    }

    private bool VerifySignature(string encodedHeader, string encodedPayload, string encodedSignature, string kid, OpenIdConfiguration openIdConfiguration)
    {
        foreach (var key in openIdConfiguration.Keys.EnumerateArray())
        {
            if (key.GetProperty("kid").GetString() != kid)
            {
                continue;
            }

            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlDecode(key.GetProperty("n").GetString()!),
                Exponent = Base64UrlDecode(key.GetProperty("e").GetString()!)
            });

            var signedData = Encoding.ASCII.GetBytes($"{encodedHeader}.{encodedPayload}");
            var signature = Base64UrlDecode(encodedSignature);
            return rsa.VerifyData(signedData, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        return false;
    }

    private static bool IsTokenInLifetime(JsonElement payload)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (payload.TryGetProperty("nbf", out var notBefore) && now < notBefore.GetInt64())
        {
            return false;
        }

        return payload.TryGetProperty("exp", out var expires) && now < expires.GetInt64();
    }

    private bool HasAudience(JsonElement payload)
    {
        var expectedAudience = keycloakOptions.CurrentValue.Audience;
        if (string.IsNullOrWhiteSpace(expectedAudience))
        {
            return true;
        }

        if (!payload.TryGetProperty("aud", out var audience))
        {
            return false;
        }

        if (audience.ValueKind == JsonValueKind.String)
        {
            return audience.GetString() == expectedAudience;
        }

        return audience.ValueKind == JsonValueKind.Array &&
            audience.EnumerateArray().Any(value => value.GetString() == expectedAudience);
    }

    private static List<Claim> CreateClaims(JsonElement payload)
    {
        var claims = new List<Claim>();
        if (payload.TryGetProperty("preferred_username", out var username))
        {
            claims.Add(new Claim(ClaimTypes.Name, username.GetString() ?? "keycloak-user"));
        }

        AddRolesFrom(payload, "realm_access", claims);
        AddResourceRoles(payload, claims);

        return claims;
    }

    private static void AddRolesFrom(JsonElement payload, string propertyName, List<Claim> claims)
    {
        if (!payload.TryGetProperty(propertyName, out var roleContainer) ||
            !roleContainer.TryGetProperty("roles", out var roles))
        {
            return;
        }

        claims.AddRange(roles.EnumerateArray()
            .Select(role => role.GetString())
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => new Claim(ClaimTypes.Role, role!)));
    }

    private static void AddResourceRoles(JsonElement payload, List<Claim> claims)
    {
        if (!payload.TryGetProperty("resource_access", out var resources))
        {
            return;
        }

        foreach (var resource in resources.EnumerateObject())
        {
            if (!resource.Value.TryGetProperty("roles", out var roles))
            {
                continue;
            }

            claims.AddRange(roles.EnumerateArray()
                .Select(role => role.GetString())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => new Claim(ClaimTypes.Role, role!)));
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private sealed record OpenIdConfiguration(string Issuer, JsonElement Keys);
}
