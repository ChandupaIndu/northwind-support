using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, SlaSettings slaSettings) : ITicketQueries
{
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var lowHours = slaSettings.GetBaseWindowHours(TicketPriority.Low);
        var mediumHours = slaSettings.GetBaseWindowHours(TicketPriority.Medium);
        var highHours = slaSettings.GetBaseWindowHours(TicketPriority.High);
        var criticalHours = slaSettings.GetBaseWindowHours(TicketPriority.Critical);
        var premiumLowHours = Math.Max(slaSettings.MinimumWindowHours, lowHours * slaSettings.PremiumCustomerMultiplier);
        var premiumMediumHours = Math.Max(slaSettings.MinimumWindowHours, mediumHours * slaSettings.PremiumCustomerMultiplier);
        var premiumHighHours = Math.Max(slaSettings.MinimumWindowHours, highHours * slaSettings.PremiumCustomerMultiplier);
        var premiumCriticalHours = Math.Max(slaSettings.MinimumWindowHours, criticalHours * slaSettings.PremiumCustomerMultiplier);
        var tickets = ApplyFilters(TicketsWithLabels(), query);
        if (query.SlaStatus is { } slaStatus)
            tickets = ApplySlaFilter(tickets, slaStatus, now);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now,
                    GetWindowHours(x.Ticket.Priority, x.Customer.Tier, lowHours, mediumHours, highHours, criticalHours,
                        premiumLowHours, premiumMediumHours, premiumHighHours, premiumCriticalHours), slaSettings.AtRiskThresholdPercent)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    private IQueryable<TicketWithLabels> ApplySlaFilter(
        IQueryable<TicketWithLabels> source,
        SlaStatus status,
        DateTime now)
    {
        if (status == SlaStatus.NotApplicable)
            return source.Where(x => x.Ticket.DueAtUtc == null);

        if (status == SlaStatus.Met)
            return source.Where(x => x.Ticket.DueAtUtc != null && x.Ticket.ResolvedAtUtc != null &&
                x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc);

        if (status == SlaStatus.Breached)
            return source.Where(x => x.Ticket.DueAtUtc != null &&
                ((x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                 (x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc < now)));

        var low = slaSettings.GetBaseWindowHours(TicketPriority.Low);
        var medium = slaSettings.GetBaseWindowHours(TicketPriority.Medium);
        var high = slaSettings.GetBaseWindowHours(TicketPriority.High);
        var critical = slaSettings.GetBaseWindowHours(TicketPriority.Critical);
        var premiumMultiplier = slaSettings.PremiumCustomerMultiplier;
        var floor = slaSettings.MinimumWindowHours;
        var threshold = slaSettings.AtRiskThresholdPercent / 100;
        var premiumLow = Math.Max(floor, low * premiumMultiplier);
        var premiumMedium = Math.Max(floor, medium * premiumMultiplier);
        var premiumHigh = Math.Max(floor, high * premiumMultiplier);
        var premiumCritical = Math.Max(floor, critical * premiumMultiplier);
        return status == SlaStatus.AtRisk
            ? source.Where(x => x.Ticket.DueAtUtc != null && x.Ticket.ResolvedAtUtc == null &&
                x.Ticket.DueAtUtc >= now && x.Ticket.DueAtUtc <= now.AddHours((x.Ticket.Priority == TicketPriority.Low
                    ? (x.Customer.Tier == CustomerTier.Premium ? premiumLow : low)
                    : x.Ticket.Priority == TicketPriority.Medium
                        ? (x.Customer.Tier == CustomerTier.Premium ? premiumMedium : medium)
                        : x.Ticket.Priority == TicketPriority.High
                            ? (x.Customer.Tier == CustomerTier.Premium ? premiumHigh : high)
                            : (x.Customer.Tier == CustomerTier.Premium ? premiumCritical : critical)) * threshold))
            : source.Where(x => x.Ticket.DueAtUtc != null && x.Ticket.ResolvedAtUtc == null &&
                x.Ticket.DueAtUtc > now.AddHours((x.Ticket.Priority == TicketPriority.Low
                    ? (x.Customer.Tier == CustomerTier.Premium ? premiumLow : low)
                    : x.Ticket.Priority == TicketPriority.Medium
                        ? (x.Customer.Tier == CustomerTier.Premium ? premiumMedium : medium)
                        : x.Ticket.Priority == TicketPriority.High
                            ? (x.Customer.Tier == CustomerTier.Premium ? premiumHigh : high)
                            : (x.Customer.Tier == CustomerTier.Premium ? premiumCritical : critical)) * threshold));
    }

    private static double GetWindowHours(
        TicketPriority priority,
        CustomerTier tier,
        double low,
        double medium,
        double high,
        double critical,
        double premiumLow,
        double premiumMedium,
        double premiumHigh,
        double premiumCritical) => priority switch
        {
            TicketPriority.Low => tier == CustomerTier.Premium ? premiumLow : low,
            TicketPriority.Medium => tier == CustomerTier.Premium ? premiumMedium : medium,
            TicketPriority.High => tier == CustomerTier.Premium ? premiumHigh : high,
            TicketPriority.Critical => tier == CustomerTier.Premium ? premiumCritical : critical,
            _ => throw new ArgumentOutOfRangeException(nameof(priority))
        };

    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(x =>
                x.Ticket.Title.ToLower().Contains(search) ||
                x.Ticket.Reference.ToLower().Contains(search) ||
                x.Customer.Name.ToLower().Contains(search));
        }

        if (query.Status is { } status)
            source = source.Where(x => x.Ticket.Status == status);

        if (query.Priority is { } priority)
            source = source.Where(x => x.Ticket.Priority == priority);

        if (query.CategoryId is { } categoryId)
            source = source.Where(x => x.Ticket.CategoryId == categoryId);

        if (query.CustomerId is { } customerId)
            source = source.Where(x => x.Ticket.CustomerId == customerId);

        if (query.AssignedAgentId is { } agentId)
            source = source.Where(x => x.Ticket.AssignedAgentId == agentId);

        if (query.UnassignedOnly)
            source = source.Where(x => x.Ticket.AssignedAgentId == null);

        return source;
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var lowHours = slaSettings.GetBaseWindowHours(TicketPriority.Low);
        var mediumHours = slaSettings.GetBaseWindowHours(TicketPriority.Medium);
        var highHours = slaSettings.GetBaseWindowHours(TicketPriority.High);
        var criticalHours = slaSettings.GetBaseWindowHours(TicketPriority.Critical);
        var premiumLowHours = Math.Max(slaSettings.MinimumWindowHours, lowHours * slaSettings.PremiumCustomerMultiplier);
        var premiumMediumHours = Math.Max(slaSettings.MinimumWindowHours, mediumHours * slaSettings.PremiumCustomerMultiplier);
        var premiumHighHours = Math.Max(slaSettings.MinimumWindowHours, highHours * slaSettings.PremiumCustomerMultiplier);
        var premiumCriticalHours = Math.Max(slaSettings.MinimumWindowHours, criticalHours * slaSettings.PremiumCustomerMultiplier);

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now,
                    GetWindowHours(x.Ticket.Priority, x.Customer.Tier, lowHours, mediumHours, highHours, criticalHours,
                        premiumLowHours, premiumMediumHours, premiumHighHours, premiumCriticalHours), slaSettings.AtRiskThresholdPercent)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var lowHours = slaSettings.GetBaseWindowHours(TicketPriority.Low);
        var mediumHours = slaSettings.GetBaseWindowHours(TicketPriority.Medium);
        var highHours = slaSettings.GetBaseWindowHours(TicketPriority.High);
        var criticalHours = slaSettings.GetBaseWindowHours(TicketPriority.Critical);
        var premiumLowHours = Math.Max(slaSettings.MinimumWindowHours, lowHours * slaSettings.PremiumCustomerMultiplier);
        var premiumMediumHours = Math.Max(slaSettings.MinimumWindowHours, mediumHours * slaSettings.PremiumCustomerMultiplier);
        var premiumHighHours = Math.Max(slaSettings.MinimumWindowHours, highHours * slaSettings.PremiumCustomerMultiplier);
        var premiumCriticalHours = Math.Max(slaSettings.MinimumWindowHours, criticalHours * slaSettings.PremiumCustomerMultiplier);

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now,
                    GetWindowHours(x.Ticket.Priority, x.Customer.Tier, lowHours, mediumHours, highHours, criticalHours,
                        premiumLowHours, premiumMediumHours, premiumHighHours, premiumCriticalHours), slaSettings.AtRiskThresholdPercent)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
