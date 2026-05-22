namespace UTB.Minute.WebApi;

public sealed class KeycloakAuthenticationOptions
{
    public string? Authority { get; set; }
    public string Audience { get; set; } = "minute-api";
    public bool AllowDebugRoleHeader { get; set; }
}
