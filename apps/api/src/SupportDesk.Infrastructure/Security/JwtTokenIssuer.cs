using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Security;

public sealed class JwtTokenIssuer(JwtSettings settings, IClock clock) : IJwtTokenIssuer
{
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public IssuedJwt Issue(Agent agent)
    {
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(settings.TokenLifetimeMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, agent.FullName),
            new Claim(JwtRegisteredClaimNames.Email, agent.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            now,
            expiresAtUtc,
            _credentials);

        return new IssuedJwt(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
