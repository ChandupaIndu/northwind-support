using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Security;

public sealed class IdentityAgentPasswordHasher(IPasswordHasher<Agent> passwordHasher) : IAgentPasswordHasher
{
    public string Hash(Agent agent, string password) => passwordHasher.HashPassword(agent, password);

    public bool Verify(Agent agent, string passwordHash, string password) =>
        passwordHasher.VerifyHashedPassword(agent, passwordHash, password) is not PasswordVerificationResult.Failed;
}
