using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Features.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Tickets;

public sealed class TicketTriageServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DecideAsync_UsesForcedCriticalAndPremiumAdjustedWindow()
    {
        var category = new Category("Security", true, true);
        var (service, _) = Create(CustomerTier.Premium, category,
        [
            Candidate(9, "Specialist", 1, 8, true, categoryId: 1),
            Candidate(2, "Not qualified", 0, 8, true)
        ]);

        var result = await service.DecideAsync(TicketPriority.Low, 1, 1, null, null, Now, CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Equal(2, result.SlaWindowHours);
        Assert.Equal(Now.AddHours(2), result.DueAtUtc);
        Assert.Equal(9, result.AssignedAgentId);
        Assert.Contains("Security", result.PriorityReason);
    }

    [Fact]
    public async Task DecideAsync_SelectsLowestOpenCountThenLowestIdAmongEligibleAgents()
    {
        var (service, _) = Create(CustomerTier.Standard, new Category("General", false, false),
        [
            Candidate(7, "Seven", 2, 8, true),
            Candidate(3, "Three", 2, 8, true),
            Candidate(1, "Full", 8, 8, true),
            Candidate(2, "Inactive", 0, 8, false)
        ]);

        var result = await service.DecideAsync(TicketPriority.Medium, 1, 1, null, null, Now, CancellationToken.None);

        Assert.Equal(3, result.AssignedAgentId);
        Assert.Equal("Three", result.AssignedAgentName);
        Assert.Equal(24, result.SlaWindowHours);
    }

    [Fact]
    public async Task DecideAsync_LeavesTicketUnassignedWhenNoSpecialistHasCapacity()
    {
        var (service, _) = Create(CustomerTier.Standard, new Category("Billing", true, false),
        [Candidate(1, "Overloaded", 4, 4, true, categoryId: 1)]);

        var result = await service.DecideAsync(TicketPriority.Medium, 1, 1, null, null, Now, CancellationToken.None);

        Assert.Null(result.AssignedAgentId);
        Assert.Contains("remains unassigned", result.AssignmentReason);
    }

    [Fact]
    public async Task DecideAsync_KeepsAnEligibleCurrentAgent()
    {
        var (service, _) = Create(CustomerTier.Standard, new Category("General", false, false),
        [Candidate(4, "Current", 3, 6, true), Candidate(2, "Less busy", 0, 6, true)]);

        var result = await service.DecideAsync(TicketPriority.High, 1, 1, 55, 4, Now, CancellationToken.None);

        Assert.Equal(4, result.AssignedAgentId);
        Assert.Contains("Kept Current", result.AssignmentReason);
    }

    [Theory]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4)]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2)]
    public async Task DecideAsync_UsesConfiguredPriorityAndCustomerTierWindows(
        TicketPriority priority,
        CustomerTier tier,
        double expectedHours)
    {
        var (service, _) = Create(tier, new Category("General", false, false), []);

        var result = await service.DecideAsync(priority, 1, 1, null, null, Now, CancellationToken.None);

        Assert.Equal(expectedHours, result.SlaWindowHours);
        Assert.Equal(Now.AddHours(expectedHours), result.DueAtUtc);
    }

    [Fact]
    public async Task DecideAsync_AppliesMinimumOneHourPremiumWindow()
    {
        var (service, _) = Create(CustomerTier.Premium, new Category("General", false, false), [], new SlaSettings
        {
            BaseWindowHours = new Dictionary<string, double>
            {
                ["Low"] = 2, ["Medium"] = 1, ["High"] = 8, ["Critical"] = 4
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1,
            AtRiskThresholdPercent = 25
        });

        var result = await service.DecideAsync(TicketPriority.Medium, 1, 1, null, null, Now, CancellationToken.None);

        Assert.Equal(1, result.SlaWindowHours);
        Assert.Equal(Now.AddHours(1), result.DueAtUtc);
    }

    private static (TicketTriageService Service, Mock<IAgentAssignmentCandidates> Candidates) Create(
        CustomerTier tier,
        Category category,
        IReadOnlyList<AgentAssignmentCandidate> agents,
        SlaSettings? settings = null)
    {
        var customerRepository = new Mock<ICustomerRepository>();
        customerRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer("Contoso Ltd", "contoso@example.test", null, tier, Now));
        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        var candidates = new Mock<IAgentAssignmentCandidates>();
        candidates.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(agents);

        settings ??= new SlaSettings
        {
            BaseWindowHours = new Dictionary<string, double>
            {
                ["Low"] = 72, ["Medium"] = 24, ["High"] = 8, ["Critical"] = 4
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1,
            AtRiskThresholdPercent = 25
        };

        return (new TicketTriageService(customerRepository.Object, categoryRepository.Object, candidates.Object, settings), candidates);
    }

    private static AgentAssignmentCandidate Candidate(
        int id,
        string name,
        int openCount,
        int limit,
        bool active,
        int? categoryId = null) =>
        new(id, name, active, limit, openCount, categoryId is null ? new HashSet<int>() : [categoryId.Value]);
}
