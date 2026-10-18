using System.Text;
using Microsoft.Extensions.Configuration;

namespace SupportDesk.Infrastructure.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public required string SigningKey { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public int TokenLifetimeMinutes { get; init; } = 60;

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var settings = new JwtSettings
        {
            SigningKey = section["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required."),
            Issuer = section["Issuer"] ?? "NorthwindSupportDesk",
            Audience = section["Audience"] ?? "NorthwindSupportDesk.Web",
            TokenLifetimeMinutes = int.TryParse(section["TokenLifetimeMinutes"], out var minutes) ? minutes : 60
        };

        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        if (Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        if (TokenLifetimeMinutes != 60)
            throw new InvalidOperationException("Jwt:TokenLifetimeMinutes must be 60 for this assessment.");
    }
}
