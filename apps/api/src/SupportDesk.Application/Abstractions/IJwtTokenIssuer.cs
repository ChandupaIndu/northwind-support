using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public sealed record IssuedJwt(string AccessToken, DateTime ExpiresAtUtc);

public interface IJwtTokenIssuer
{
    IssuedJwt Issue(Agent agent);
}
