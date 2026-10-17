using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketEscalationMigrationTests
{
    [Fact]
    public void SqlServerMigrationScript_CreatesEscalationHistoryAndIndexes()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=unused;Database=unused;TrustServerCertificate=True")
            .Options;
        using var db = new SupportDbContext(options);

        var script = db.GetService<IMigrator>().GenerateScript();

        Assert.Contains("CREATE TABLE [TicketEscalations]", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_TicketEscalations_TicketId_EscalatedAtUtc", script);
        Assert.Contains("FK_TicketEscalations_Tickets_TicketId", script);
    }
}
