namespace SupportDesk.Application.Abstractions;

public sealed record AgentAssignmentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlySet<int> SpecializationCategoryIds);

public interface IAgentAssignmentCandidates
{
    Task<IReadOnlyList<AgentAssignmentCandidate>> GetAsync(int? excludingTicketId, CancellationToken ct);
}
