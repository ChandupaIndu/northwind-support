using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public sealed class JwtTokenIssuerTests
{
    [Fact]
    public void Issue_ProducesSignedTokenWithAgentClaimsAndSixtyMinuteLifetime()
    {
        const string key = "test-only-northwind-jwt-signing-key-32-bytes-minimum";
        var settings = new JwtSettings
        {
            SigningKey = key,
            Issuer = "test-issuer",
            Audience = "test-audience",
            TokenLifetimeMinutes = 60
        };
        var now = FixedClock.DefaultNow;
        var agent = new Agent("Alex Turner", "alex@example.test", 10, now);
        var issued = new JwtTokenIssuer(settings, new FixedClock(now)).Issue(agent);
        var validator = new JwtSecurityTokenHandler();
        validator.MapInboundClaims = false;
        var principal = validator.ValidateToken(issued.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            // The issuer uses the injected fixed clock; wall-clock lifetime validation would
            // make this test depend on the machine date instead of the token's configured expiry.
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        }, out var validatedToken);

        Assert.Equal(agent.Id.ToString(), principal.FindFirst("sub")?.Value);
        Assert.Equal(agent.FullName, principal.FindFirst("name")?.Value);
        Assert.Equal(agent.Email, principal.FindFirst("email")?.Value);
        Assert.Equal(now.AddMinutes(60), issued.ExpiresAtUtc);
        Assert.True(validatedToken is JwtSecurityToken);
    }
}
