using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_UnknownEmailReturnsNoToken()
    {
        var agents = new Mock<IAgentRepository>();
        agents.Setup(repository => repository.GetByEmailAsync("unknown@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);
        var tokens = new Mock<IJwtTokenIssuer>();
        var handler = new LoginCommandHandler(agents.Object, new Mock<IAgentPasswordHasher>().Object, tokens.Object);

        var result = await handler.HandleAsync(
            new LoginRequest("unknown@example.test", "some-password"), CancellationToken.None);

        Assert.Null(result);
        tokens.Verify(issuer => issuer.Issue(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WrongPasswordReturnsNoToken()
    {
        var agent = ActiveAgent();
        var agents = RepositoryReturning(agent);
        var passwords = new Mock<IAgentPasswordHasher>();
        passwords.Setup(hasher => hasher.Verify(agent, "stored-hash", "wrong-password")).Returns(false);
        var tokens = new Mock<IJwtTokenIssuer>();
        var handler = new LoginCommandHandler(agents.Object, passwords.Object, tokens.Object);

        var result = await handler.HandleAsync(
            new LoginRequest(agent.Email, "wrong-password"), CancellationToken.None);

        Assert.Null(result);
        tokens.Verify(issuer => issuer.Issue(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InactiveAgentCannotReceiveToken()
    {
        var agent = ActiveAgent();
        agent.Deactivate();
        var agents = RepositoryReturning(agent);
        var passwords = new Mock<IAgentPasswordHasher>();
        var tokens = new Mock<IJwtTokenIssuer>();
        var handler = new LoginCommandHandler(agents.Object, passwords.Object, tokens.Object);

        var result = await handler.HandleAsync(
            new LoginRequest(agent.Email, "correct-password"), CancellationToken.None);

        Assert.Null(result);
        passwords.Verify(hasher => hasher.Verify(It.IsAny<Agent>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        tokens.Verify(issuer => issuer.Issue(It.IsAny<Agent>()), Times.Never);
    }

    private static Agent ActiveAgent()
    {
        var agent = new Agent("Alex Turner", "alex@example.test", 10, DateTime.UtcNow);
        agent.SetPasswordHash("stored-hash");
        return agent;
    }

    private static Mock<IAgentRepository> RepositoryReturning(Agent agent)
    {
        var repository = new Mock<IAgentRepository>();
        repository.Setup(value => value.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        return repository;
    }
}
