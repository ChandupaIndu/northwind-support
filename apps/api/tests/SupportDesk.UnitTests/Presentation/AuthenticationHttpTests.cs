using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using Xunit;

namespace SupportDesk.UnitTests.Presentation;

[Collection(AuthHttpTestCollection.Name)]
public sealed class AuthenticationHttpTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>, IDisposable
{
    private readonly HttpClient _client = InitializeClient(factory);

    [Fact]
    public async Task Login_UnknownEmailAndWrongPasswordReturnSameUnauthorizedResponse()
    {
        var unknownEmail = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "missing@example.test",
            password = AuthApiFactory.AgentPassword
        });
        var wrongPassword = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AuthApiFactory.AgentEmail,
            password = "incorrect-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var unknownJson = await unknownEmail.Content.ReadAsStringAsync();
        var wrongJson = await wrongPassword.Content.ReadAsStringAsync();
        Assert.Equal(unknownJson, wrongJson);

        using var unknownBody = JsonDocument.Parse(unknownJson);
        Assert.Equal("Invalid email or password.", unknownBody.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Me_RequiresJwtAndReturnsAgentFromValidLoginToken()
    {
        var unauthorized = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var token = await LoginAndGetTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var agent = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(factory.AgentId, agent.GetProperty("id").GetInt32());
        Assert.Equal(AuthApiFactory.AgentName, agent.GetProperty("fullName").GetString());
        Assert.Equal(AuthApiFactory.AgentEmail, agent.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ExistingTicketsEndpoint_RequiresJwtAndAcceptsValidJwt()
    {
        var unauthorized = await _client.GetAsync("/api/tickets");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var token = await LoginAndGetTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tickets");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose() => _client.Dispose();

    private async Task<string> LoginAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AuthApiFactory.AgentEmail,
            password = AuthApiFactory.AgentPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private static HttpClient InitializeClient(AuthApiFactory factory)
    {
        var settings = AuthApiFactory.Configuration;
        var previous = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var setting in settings)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);

            var client = factory.CreateClient();
            factory.EnsureDatabaseCreated();
            return client;
        }
        finally
        {
            foreach (var setting in previous)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }
}

[CollectionDefinition(AuthHttpTestCollection.Name, DisableParallelization = true)]
public sealed class AuthHttpTestCollection
{
    public const string Name = "Authentication HTTP integration tests";
}

public sealed class AuthApiFactory : WebApplicationFactory<SupportDesk.Presentation.Controllers.AuthController>
{
    public const string AgentName = "Test Agent";
    public const string AgentEmail = "agent@example.test";
    public const string AgentPassword = "test-only-password";

    private const string SigningKey = "test-only-northwind-auth-http-signing-key-with-32-bytes-minimum";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private bool _databaseInitialized;

    public static IReadOnlyDictionary<string, string> Configuration { get; } = new Dictionary<string, string>
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Testing",
        ["ConnectionStrings__Default"] = "Server=localhost;Database=isolated-test-only",
        ["Jwt__SigningKey"] = SigningKey,
        ["Jwt__Issuer"] = "NorthwindSupportDesk.Tests",
        ["Jwt__Audience"] = "NorthwindSupportDesk.Tests.Web",
        ["Jwt__TokenLifetimeMinutes"] = "60",
        ["Sla__BaseWindowHours__Low"] = "72",
        ["Sla__BaseWindowHours__Medium"] = "24",
        ["Sla__BaseWindowHours__High"] = "8",
        ["Sla__BaseWindowHours__Critical"] = "4",
        ["Sla__PremiumCustomerMultiplier"] = "0.5",
        ["Sla__MinimumWindowHours"] = "1",
        ["Sla__AtRiskThresholdPercent"] = "25"
    };

    public AuthApiFactory()
    {
        _connection.Open();
    }

    public int AgentId { get; private set; }

    public void EnsureDatabaseCreated()
    {
        if (_databaseInitialized) return;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        db.Database.EnsureCreated();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IAgentPasswordHasher>();
        var agent = new Agent(AgentName, AgentEmail, 10, DateTime.UtcNow);
        agent.SetPasswordHash(passwordHasher.Hash(agent, AgentPassword));
        db.Add(agent);
        db.SaveChanges();
        AgentId = agent.Id;
        _databaseInitialized = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<SupportDbContext>();
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
