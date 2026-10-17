using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets;

/// <summary>Applies the shared priority, SLA and agent-selection rules used at creation and escalation.</summary>
public sealed class TicketTriageService(
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentAssignmentCandidates candidates,
    SlaSettings settings) : ITicketTriageService
{
    public async Task<TriageDecision> DecideAsync(
        TicketPriority requestedPriority,
        int customerId,
        int categoryId,
        int? excludingTicketId,
        int? currentAgentId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(customerId, ct)
            ?? throw new InvalidOperationException($"Customer {customerId} disappeared during triage.");
        var category = await categories.GetByIdAsync(categoryId, ct)
            ?? throw new InvalidOperationException($"Category {categoryId} disappeared during triage.");

        var priority = category.ForcesCriticalPriority ? TicketPriority.Critical : requestedPriority;
        var priorityReason = category.ForcesCriticalPriority
            ? $"{category.Name} requires Critical priority."
            : $"Requested {requestedPriority} priority was applied.";
        var baseHours = settings.GetBaseWindowHours(priority);
        var adjustedHours = customer.Tier == CustomerTier.Premium
            ? Math.Max(settings.MinimumWindowHours, baseHours * settings.PremiumCustomerMultiplier)
            : baseHours;
        var slaReason = customer.Tier == CustomerTier.Premium
            ? $"{priority} starts at {baseHours:g} hours; the Premium adjustment gives {adjustedHours:g} hours (minimum {settings.MinimumWindowHours:g} hour)."
            : $"{priority} uses the configured {adjustedHours:g}-hour SLA window.";

        var agents = await candidates.GetAsync(excludingTicketId, ct);
        bool Eligible(AgentAssignmentCandidate agent) =>
            agent.IsActive && agent.OpenTicketCount < agent.MaxOpenTickets &&
            (!category.RequiresSpecialist || agent.SpecializationCategoryIds.Contains(categoryId));

        var currentAgent = currentAgentId is null ? null : agents.FirstOrDefault(a => a.Id == currentAgentId);
        var selected = currentAgent is not null && Eligible(currentAgent)
            ? currentAgent
            : agents.Where(Eligible).OrderBy(a => a.OpenTicketCount).ThenBy(a => a.Id).FirstOrDefault();

        var assignmentReason = selected is null
            ? category.RequiresSpecialist
                ? $"No active specialist for {category.Name} has capacity; ticket remains unassigned."
                : "No active agent has capacity; ticket remains unassigned."
            : currentAgent == selected
                ? $"Kept {selected.FullName}; the agent is active, qualified, and has capacity."
                : $"Assigned to {selected.FullName}, the eligible agent with the fewest open tickets (ties use the lowest agent ID).";

        return new TriageDecision(
            priority,
            priorityReason,
            adjustedHours,
            slaReason,
            assignmentReason,
            selected?.Id,
            selected?.FullName,
            nowUtc.AddHours(adjustedHours),
            SlaEvaluator.Evaluate(nowUtc.AddHours(adjustedHours), null, nowUtc, adjustedHours, settings.AtRiskThresholdPercent));
    }
}
