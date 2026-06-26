using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EGWNCentral.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EGWNCentral.Api.Services;

public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions)
{
    public (string token, DateTimeOffset expiresAtUtc) CreateToken(string username)
    {
        var options = jwtOptions.Value;
        var now = DateTimeOffset.UtcNow;
        var expiresAtUtc = now.AddMinutes(options.ExpirationMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(tokenDescriptor), expiresAtUtc);
    }
}
