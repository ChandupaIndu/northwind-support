using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public interface IAgentPasswordHasher
{
    string Hash(Agent agent, string password);

    bool Verify(Agent agent, string passwordHash, string password);
}
