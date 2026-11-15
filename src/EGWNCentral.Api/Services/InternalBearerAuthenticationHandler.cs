using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using EGWNCentral.Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EGWNCentral.Api.Services;

public static class InternalBearerDefaults
{
    public const string AuthenticationScheme = "InternalBearer";
}

public sealed class InternalBearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<InternalRegistrationOptions> internalOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization;
        if (authorization.Count != 1 ||
            !AuthenticationHeaderValue.TryParse(authorization[0], out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(header.Parameter))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = Encoding.UTF8.GetBytes(internalOptions.CurrentValue.Token);
        var supplied = Encoding.UTF8.GetBytes(header.Parameter);
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal bearer token."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "keycloak")], Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
