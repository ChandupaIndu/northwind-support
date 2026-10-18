using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(
    ITicketRepository tickets,
    ITicketEscalationQueries escalations)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct)
    {
        if (await tickets.GetByIdAsync(ticketId, ct) is null)
            throw new NotFoundException("Ticket", ticketId);

        return await escalations.GetHistoryAsync(ticketId, ct);
    }
}
