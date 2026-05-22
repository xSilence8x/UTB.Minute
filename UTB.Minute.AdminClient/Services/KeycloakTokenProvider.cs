using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace UTB.Minute.AdminClient.Services;

public sealed class KeycloakTokenProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private string? accessToken;

    public bool IsSignedIn => !string.IsNullOrWhiteSpace(accessToken);
    public string? LastError { get; private set; }

    public async Task<string?> SignInAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        LastError = null;
        accessToken = await RequestTokenAsync(username, password, cancellationToken);
        return accessToken;
    }

    public void SignOut() => accessToken = null;

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return accessToken;
        }

        return null;
    }

    private async Task<string?> RequestTokenAsync(string username, string password, CancellationToken cancellationToken)
    {
        var authority = GetAuthority();
        if (string.IsNullOrWhiteSpace(authority))
        {
            LastError = "Keycloak authority is not configured.";
            return null;
        }

        var tokenEndpoint = $"{authority.TrimEnd('/')}/protocol/openid-connect/token";
        var clientId = configuration["Keycloak:ClientId"] ?? "minute-client";
        var client = httpClientFactory.CreateClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = clientId,
                    ["scope"] = "openid",
                    ["username"] = username,
                    ["password"] = password
                });

                var response = await client.PostAsync(tokenEndpoint, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    LastError = $"Keycloak token request failed with {(int)response.StatusCode}: {body}";

                    if (!IsTransient(response.StatusCode) || attempt == 5)
                    {
                        return null;
                    }
                }
                else
                {
                    var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
                    return token?.AccessToken;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                LastError = ex.Message;
                if (attempt == 5)
                {
                    return null;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken);
        }

        return null;
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode) =>
        statusCode is System.Net.HttpStatusCode.RequestTimeout or
            System.Net.HttpStatusCode.TooManyRequests or
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout ||
        (int)statusCode >= 500;

    private string? GetAuthority()
    {
        var configured = configuration["Keycloak:Authority"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var endpoint = configuration["services:keycloak:http:0"] ?? configuration["Services:keycloak:http:0"];
        return string.IsNullOrWhiteSpace(endpoint) ? null : $"{endpoint.TrimEnd('/')}/realms/minute";
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }
}
