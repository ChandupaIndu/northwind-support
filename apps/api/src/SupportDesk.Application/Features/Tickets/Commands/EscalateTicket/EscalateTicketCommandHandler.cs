using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    IAgentRepository agents,
    ITicketTriageService triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket)
{
    public async Task<EscalateTicketResultDto> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
            throw new ConflictException($"Ticket {ticket.Reference} cannot be escalated because it is {ticket.Status}.");
        if (ticket.Priority == TicketPriority.Critical)
            throw new ConflictException($"Ticket {ticket.Reference} is already Critical and cannot be escalated further.");

        var now = clock.UtcNow;
        var fromPriority = ticket.Priority;
        var fromAgentId = ticket.AssignedAgentId;
        var fromDueAtUtc = ticket.DueAtUtc;
        var fromAgent = fromAgentId is null ? null : await agents.GetByIdAsync(fromAgentId.Value, ct);
        var toPriority = (TicketPriority)((int)fromPriority + 1);
        var decision = await triage.DecideAsync(
            toPriority,
            ticket.CustomerId,
            ticket.CategoryId,
            ticket.Id,
            fromAgentId,
            now,
            ct);

        // The aggregate owns the conflict rules and independently rejects closed states or Critical.
        ticket.Escalate(now, decision.DueAtUtc, decision.AssignedAgentId);
        var history = TicketEscalation.Record(
            ticket.Id,
            fromPriority,
            ticket.Priority,
            fromAgentId,
            ticket.AssignedAgentId,
            fromDueAtUtc,
            ticket.DueAtUtc,
            request.Reason!,
            request.EscalatedBy!,
            now);
        await tickets.AddEscalationAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var updatedTicket = await getTicket.HandleAsync(ticket.Id, ct);
        return new EscalateTicketResultDto(
            updatedTicket,
            new TicketEscalationDto(
                history.Id,
                history.TicketId,
                history.FromPriority,
                history.ToPriority,
                history.FromAgentId,
                fromAgent?.FullName,
                history.ToAgentId,
                decision.AssignedAgentName,
                history.FromDueAtUtc,
                history.ToDueAtUtc,
                history.Reason,
                history.EscalatedBy,
                history.EscalatedAtUtc));
    }
}
