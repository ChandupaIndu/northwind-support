using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class EscalateTicketCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_EscalatesAndPersistsTheBeforeAndAfterValues()
    {
        var now = FixedClock.DefaultNow;
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(7).Build();
        ticket.ApplyTriage(TicketPriority.Medium, now.AddHours(10), 7, now);
        var tickets = new Mock<ITicketRepository>();
        tickets.Setup(x => x.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        TicketEscalation? savedHistory = null;
        tickets.Setup(x => x.AddEscalationAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()))
            .Callback<TicketEscalation, CancellationToken>((entry, _) => savedHistory = entry)
            .Returns(Task.CompletedTask);
        var agents = new Mock<IAgentRepository>();
        agents.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Agent("Ada Lovelace", "ada@example.test", 8, now));
        var triage = new Mock<ITicketTriageService>();
        triage.Setup(x => x.DecideAsync(TicketPriority.High, ticket.CustomerId, ticket.CategoryId, ticket.Id, 7,
                now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriageDecision(TicketPriority.High, "Escalated", 8, "High SLA", "Reassigned by rule.",
                9, "Grace Hopper", now.AddHours(8), SlaStatus.WithinSla));
        var queries = new Mock<ITicketQueries>();
        queries.Setup(x => x.GetDetailAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto { Id = ticket.Id, Priority = TicketPriority.High });
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new EscalateTicketCommandHandler(
            tickets.Object, agents.Object, triage.Object, unitOfWork.Object, new FixedClock(now),
            new GetTicketQueryHandler(queries.Object));

        var result = await handler.HandleAsync(ticket.Id,
            new EscalateTicketRequest("Customer impact increased."), "Team lead", CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(now.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(9, ticket.AssignedAgentId);
        Assert.NotNull(savedHistory);
        Assert.Equal(TicketPriority.Medium, savedHistory.FromPriority);
        Assert.Equal(TicketPriority.High, savedHistory.ToPriority);
        Assert.Equal(7, savedHistory.FromAgentId);
        Assert.Equal(9, savedHistory.ToAgentId);
        Assert.Equal("Customer impact increased.", savedHistory.Reason);
        Assert.Equal("Team lead", savedHistory.EscalatedBy);
        Assert.Equal("Ada Lovelace", result.Escalation.FromAgentName);
        Assert.Equal("Grace Hopper", result.Escalation.ToAgentName);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.Open)]
    [InlineData(TicketPriority.High, TicketStatus.Resolved)]
    [InlineData(TicketPriority.High, TicketStatus.Closed)]
    public async Task HandleAsync_WhenTicketIsNotEscalatable_ConflictsWithoutSaving(
        TicketPriority priority,
        TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(priority).WithStatus(status).Build();
        var tickets = new Mock<ITicketRepository>();
        tickets.Setup(x => x.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var triage = new Mock<ITicketTriageService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new EscalateTicketCommandHandler(
            tickets.Object, new Mock<IAgentRepository>().Object, triage.Object, unitOfWork.Object,
            new FixedClock(), new GetTicketQueryHandler(new Mock<ITicketQueries>().Object));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(ticket.Id, new EscalateTicketRequest("Valid reason"), "Lead", CancellationToken.None));

        triage.Verify(x => x.DecideAsync(It.IsAny<TicketPriority>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Validator_RejectsShortReason()
    {
        var validator = new EscalateTicketRequestValidator();

        var result = await validator.ValidateAsync(new EscalateTicketRequest("bad"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Reason");
    }
}
