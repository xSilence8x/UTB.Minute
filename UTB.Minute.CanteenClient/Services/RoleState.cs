namespace UTB.Minute.CanteenClient.Services;

public sealed class RoleState
{
    private bool signedIn;

    public string CurrentRole { get; set; } = "Student";
    public string Username { get; set; } = "student";
    public string Password { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public bool IsSignedIn => signedIn;

    public void SignIn(string role, string username, string password, string? accessToken)
    {
        CurrentRole = role;
        Username = username;
        Password = password;
        AccessToken = accessToken;
        signedIn = true;
    }

    public void SignOut()
    {
        AccessToken = null;
        Password = string.Empty;
        signedIn = false;
    }
}
