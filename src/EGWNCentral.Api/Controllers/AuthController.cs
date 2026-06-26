using EGWNCentral.Api.Contracts;
using EGWNCentral.Api.Options;
using EGWNCentral.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EGWNCentral.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IOptions<AuthOptions> authOptions, JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("token")]
    public ActionResult<AuthTokenResponse> CreateToken([FromBody] AuthTokenRequest request)
    {
        var options = authOptions.Value;
        if (request.Username != options.Username || request.Password != options.Password)
        {
            return Unauthorized();
        }

        var (token, expiresAtUtc) = jwtTokenService.CreateToken(request.Username);
        return Ok(new AuthTokenResponse(token, expiresAtUtc));
    }
}
