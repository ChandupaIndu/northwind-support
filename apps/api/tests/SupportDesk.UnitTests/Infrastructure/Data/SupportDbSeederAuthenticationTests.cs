using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

public sealed class SupportDbSeederAuthenticationTests
{
    [Fact]
    public async Task SeedAsync_ProvisionsMissingCredentialsEvenWhenTicketsAlreadyExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options;
        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var passwordHasher = new IdentityAgentPasswordHasher(new PasswordHasher<Agent>());
        var seeder = new SupportDbSeeder(db, new FixedClock(), passwordHasher,
            NullLogger<SupportDbSeeder>.Instance);

        await seeder.SeedAsync();
        Assert.True(await db.Set<Ticket>().AnyAsync());

        foreach (var agent in await db.Set<Agent>().ToListAsync())
            db.Entry(agent).Property(agent => agent.PasswordHash).CurrentValue = null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await seeder.SeedAsync();

        var agents = await db.Set<Agent>().ToListAsync();
        Assert.Equal(6, agents.Count);
        Assert.All(agents, agent =>
        {
            Assert.False(string.IsNullOrWhiteSpace(agent.PasswordHash));
            Assert.NotEqual(SupportDbSeeder.DevelopmentAgentPassword, agent.PasswordHash);
            Assert.True(passwordHasher.Verify(agent, agent.PasswordHash!, SupportDbSeeder.DevelopmentAgentPassword));
        });
        Assert.Equal(40, await db.Set<Ticket>().CountAsync());
    }
}
