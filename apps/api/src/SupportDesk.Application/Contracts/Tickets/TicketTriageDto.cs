using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed record TicketTriageDto(
    TicketPriority Priority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    string AssignmentReason,
    AgentSummaryDto? AssignedAgent,
    DateTime? DueAtUtc,
    SlaStatus SlaStatus);

public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    int? FromAgentId,
    string? FromAgentName,
    int? ToAgentId,
    string? ToAgentName,
    DateTime? FromDueAtUtc,
    DateTime? ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

public sealed record EscalateTicketResultDto(TicketDetailDto Ticket, TicketEscalationDto Escalation);
