namespace EGWNCentral.Api.Contracts;

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
