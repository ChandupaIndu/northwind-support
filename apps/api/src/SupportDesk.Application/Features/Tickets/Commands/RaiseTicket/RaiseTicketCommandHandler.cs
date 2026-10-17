using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Features.Tickets;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    ITicketTriageService triage,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var now = clock.UtcNow;
        var decision = await triage.DecideAsync(
            request.RequestedPriority ?? TicketPriority.Medium,
            request.CustomerId,
            request.CategoryId,
            excludingTicketId: null,
            currentAgentId: null,
            now,
            ct);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            decision.Priority,
            now);
        ticket.ApplyTriage(decision.Priority, decision.DueAtUtc, decision.AssignedAgentId, now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId);

        var result = await getTicket.HandleAsync(ticket.Id, ct);
        result.Triage = new TicketTriageDto(
            decision.Priority,
            decision.PriorityReason,
            decision.SlaWindowHours,
            decision.SlaReason,
            decision.AssignmentReason,
            decision.AssignedAgentId is null ? null : new AgentSummaryDto(decision.AssignedAgentId.Value, decision.AssignedAgentName!),
            decision.DueAtUtc,
            decision.SlaStatus);
        return result;
    }
}
