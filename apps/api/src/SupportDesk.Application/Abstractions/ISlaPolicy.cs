using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Abstractions;

public sealed class SlaSettings
{
    public Dictionary<string, double> BaseWindowHours { get; init; } = new();
    public double PremiumCustomerMultiplier { get; init; } = 0.5;
    public double MinimumWindowHours { get; init; } = 1;
    public double AtRiskThresholdPercent { get; init; } = 25;

    public double GetBaseWindowHours(TicketPriority priority) =>
        BaseWindowHours.TryGetValue(priority.ToString(), out var hours)
            ? hours
            : throw new InvalidOperationException($"No SLA window configured for priority {priority}.");
}

public sealed record TriageDecision(
    TicketPriority Priority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    string AssignmentReason,
    int? AssignedAgentId,
    string? AssignedAgentName,
    DateTime DueAtUtc,
    SlaStatus SlaStatus);

public interface ITicketTriageService
{
    Task<TriageDecision> DecideAsync(
        TicketPriority requestedPriority,
        int customerId,
        int categoryId,
        int? excludingTicketId,
        int? currentAgentId,
        DateTime nowUtc,
        CancellationToken ct);
}
