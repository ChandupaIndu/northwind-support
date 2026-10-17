using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class TicketEscalationQueries(SupportDbContext db) : ITicketEscalationQueries
{
    public async Task<IReadOnlyList<TicketEscalationDto>> GetHistoryAsync(int ticketId, CancellationToken ct) =>
        await (from history in db.Set<TicketEscalation>().AsNoTracking()
               where history.TicketId == ticketId
               join fromAgentRow in db.Set<Agent>() on history.FromAgentId equals fromAgentRow.Id into fromAgents
               from fromAgent in fromAgents.DefaultIfEmpty()
               join toAgentRow in db.Set<Agent>() on history.ToAgentId equals toAgentRow.Id into toAgents
               from toAgent in toAgents.DefaultIfEmpty()
               orderby history.EscalatedAtUtc descending, history.Id descending
               select new TicketEscalationDto(
                   history.Id,
                   history.TicketId,
                   history.FromPriority,
                   history.ToPriority,
                   history.FromAgentId,
                   fromAgent == null ? null : fromAgent.FullName,
                   history.ToAgentId,
                   toAgent == null ? null : toAgent.FullName,
                   history.FromDueAtUtc,
                   history.ToDueAtUtc,
                   history.Reason,
                   history.EscalatedBy,
                   history.EscalatedAtUtc))
            .ToListAsync(ct);
}
