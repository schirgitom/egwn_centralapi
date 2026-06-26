namespace EGWNCentral.Api.Contracts;

public sealed class AuthTokenRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
