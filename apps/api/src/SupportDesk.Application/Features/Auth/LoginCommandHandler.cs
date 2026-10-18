using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IAgentPasswordHasher passwordHasher,
    IJwtTokenIssuer tokenIssuer)
{
    public async Task<LoginResponse?> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim();
        var password = request.Password;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var agent = await agents.GetByEmailAsync(email, ct);
        if (agent is null || !agent.IsActive || agent.PasswordHash is not { } passwordHash ||
            !passwordHasher.Verify(agent, passwordHash, password))
        {
            return null;
        }

        var token = tokenIssuer.Issue(agent);
        return new LoginResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
