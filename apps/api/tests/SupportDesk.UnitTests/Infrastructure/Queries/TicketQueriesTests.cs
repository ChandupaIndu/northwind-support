using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private SupportDbContext _db = null!;
    private Ticket[] _tickets = null!;
    private int _customerOneId;
    private int _customerTwoId;
    private int _categoryOneId;
    private int _categoryTwoId;
    private int _agentOneId;
    private TicketQueries _queries = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        var customerOne = new Customer("Acme Corporation", "acme@example.test", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var customerTwo = new Customer("Northwind Traders", "northwind@example.test", null, CustomerTier.Premium, FixedClock.DefaultNow);
        var categoryOne = new Category("Billing", false, false);
        var categoryTwo = new Category("Outage", true, false);
        var agentOne = new Agent("Ada Lovelace", "ada@example.test", 10, FixedClock.DefaultNow);
        var agentTwo = new Agent("Grace Hopper", "grace@example.test", 10, FixedClock.DefaultNow);
        _db.AddRange(customerOne, customerTwo, categoryOne, categoryTwo, agentOne, agentTwo);
        await _db.SaveChangesAsync();

        _customerOneId = customerOne.Id;
        _customerTwoId = customerTwo.Id;
        _categoryOneId = categoryOne.Id;
        _categoryTwoId = categoryTwo.Id;
        _agentOneId = agentOne.Id;
        _tickets =
        [
            Ticket.Raise("TCK-1001", "Payment failed", "Description", _customerOneId, _categoryOneId, TicketPriority.High, FixedClock.DefaultNow.AddDays(-4)),
            Ticket.Raise("TCK-1002", "Cannot sign in", "Description", _customerTwoId, _categoryTwoId, TicketPriority.Critical, FixedClock.DefaultNow.AddDays(-3)),
            Ticket.Raise("TCK-1003", "Invoice question", "Description", _customerOneId, _categoryTwoId, TicketPriority.High, FixedClock.DefaultNow.AddDays(-2)),
            Ticket.Raise("TCK-1004", "Service unavailable", "Description", _customerTwoId, _categoryOneId, TicketPriority.Low, FixedClock.DefaultNow.AddDays(-1))
        ];
        _tickets[0].ChangeStatus(TicketStatus.InProgress, FixedClock.DefaultNow);
        _tickets[1].ChangeStatus(TicketStatus.Open, FixedClock.DefaultNow);
        _tickets[3].AssignTo(agentOne.Id, FixedClock.DefaultNow);
        _tickets[3].ChangeStatus(TicketStatus.Resolved, FixedClock.DefaultNow);
        _db.AddRange(_tickets);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        _queries = new TicketQueries(_db, new FixedClock());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData("payment", "TCK-1001")]
    [InlineData("1002", "TCK-1002")]
    [InlineData("ACME", "TCK-1003,TCK-1001")]
    public async Task Search_matches_title_reference_or_customer_name(string search, string expectedReferences)
    {
        var result = await Get(new TicketQuery { Search = search });

        Assert.Equal(expectedReferences.Split(','), result.Items.Select(x => x.Reference));
    }

    [Fact]
    public async Task Filters_by_each_supported_field()
    {
        Assert.Equal(["TCK-1003"], (await Get(new TicketQuery { Status = TicketStatus.New })).Items.Select(x => x.Reference));
        Assert.Equal(["TCK-1001"], (await Get(new TicketQuery { Status = TicketStatus.InProgress })).Items.Select(x => x.Reference));
        Assert.Equal(["TCK-1003", "TCK-1001"], (await Get(new TicketQuery { Priority = TicketPriority.High })).Items.Select(x => x.Reference));
        Assert.Equal(["TCK-1004", "TCK-1001"], (await Get(new TicketQuery { CategoryId = _categoryOneId })).Items.Select(x => x.Reference));
        Assert.Equal(["TCK-1003", "TCK-1001"], (await Get(new TicketQuery { CustomerId = _customerOneId })).Items.Select(x => x.Reference));
        Assert.Equal(["TCK-1004"], (await Get(new TicketQuery { AssignedAgentId = _agentOneId })).Items.Select(x => x.Reference));
    }

    [Fact]
    public async Task Combines_filters_with_and_and_unassigned_only()
    {
        var result = await Get(new TicketQuery
        {
            CustomerId = _customerTwoId,
            CategoryId = _categoryTwoId,
            UnassignedOnly = true
        });

        Assert.Equal(["TCK-1002"], result.Items.Select(x => x.Reference));
        Assert.Null(result.Items[0].AssignedAgent);
    }

    [Fact]
    public async Task Counts_filtered_rows_before_pagination_and_keeps_stable_sort_order()
    {
        var result = await Get(new TicketQuery { CustomerId = _customerOneId, Page = 2, PageSize = 1, SortBy = "createdAtUtc", SortDirection = "asc" });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(["TCK-1003"], result.Items.Select(x => x.Reference));

        var tied = await Get(new TicketQuery { SortBy = "priority", SortDirection = "desc" });
        Assert.Equal(["TCK-1002", "TCK-1003", "TCK-1001", "TCK-1004"], tied.Items.Select(x => x.Reference));
    }

    private Task<SupportDesk.Application.Contracts.Common.PagedResult<TicketListItemDto>> Get(TicketQuery query) =>
        _queries.GetPagedAsync(query, CancellationToken.None);
}
