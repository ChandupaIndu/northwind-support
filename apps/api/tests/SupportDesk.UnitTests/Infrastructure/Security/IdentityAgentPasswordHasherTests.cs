using Microsoft.AspNetCore.Identity;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Security;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public sealed class IdentityAgentPasswordHasherTests
{
    [Fact]
    public void HashAndVerify_UsesSaltedHashAndAcceptsOnlyTheOriginalPassword()
    {
        var agent = new Agent("Alex Turner", "alex@example.test", 10, DateTime.UtcNow);
        var hasher = new IdentityAgentPasswordHasher(new PasswordHasher<Agent>());

        var encodedHash = hasher.Hash(agent, "Northwind-Local-Only-2026!");

        Assert.NotEqual("Northwind-Local-Only-2026!", encodedHash);
        Assert.True(hasher.Verify(agent, encodedHash, "Northwind-Local-Only-2026!"));
        Assert.False(hasher.Verify(agent, encodedHash, "incorrect-password"));
    }
}
