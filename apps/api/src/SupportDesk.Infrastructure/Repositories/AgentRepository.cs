using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository, IAgentAssignmentCandidates
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        return db.Set<Agent>()
            .FirstOrDefaultAsync(agent => agent.Email.ToUpper() == normalizedEmail, ct);
    }

    public async Task<IReadOnlyList<AgentAssignmentCandidate>> GetAsync(int? excludingTicketId, CancellationToken ct)
    {
        var agents = await db.Set<Agent>().AsNoTracking().Include(a => a.Specializations).ToListAsync(ct);
        var counts = await db.Set<Ticket>().AsNoTracking()
            .Where(t => t.AssignedAgentId != null && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed &&
                (excludingTicketId == null || t.Id != excludingTicketId))
            .GroupBy(t => t.AssignedAgentId!.Value)
            .Select(group => new { AgentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.AgentId, x => x.Count, ct);

        return agents.Select(agent => new AgentAssignmentCandidate(
            agent.Id,
            agent.FullName,
            agent.IsActive,
            agent.MaxOpenTickets,
            counts.GetValueOrDefault(agent.Id),
            agent.Specializations.Select(s => s.CategoryId).ToHashSet())).ToArray();
    }
}
