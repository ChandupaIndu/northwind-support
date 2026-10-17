using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Features.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        var sla = configuration.GetSection("Sla");
        var windowSettings = sla.GetSection("BaseWindowHours").GetChildren()
            .ToDictionary(section => section.Key, section => double.Parse(section.Value!, CultureInfo.InvariantCulture));
        services.AddSingleton(new SlaSettings
        {
            BaseWindowHours = windowSettings,
            PremiumCustomerMultiplier = ReadSlaNumber(sla, "PremiumCustomerMultiplier", 0.5),
            MinimumWindowHours = ReadSlaNumber(sla, "MinimumWindowHours", 1),
            AtRiskThresholdPercent = ReadSlaNumber(sla, "AtRiskThresholdPercent", 25)
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<AgentRepository>();
        services.AddScoped<IAgentRepository>(sp => sp.GetRequiredService<AgentRepository>());
        services.AddScoped<IAgentAssignmentCandidates>(sp => sp.GetRequiredService<AgentRepository>());
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ITicketEscalationQueries, TicketEscalationQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        services.AddScoped<SupportDbSeeder>();

        return services;
    }

    private static double ReadSlaNumber(IConfigurationSection section, string key, double fallback) =>
        double.TryParse(section[key], CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
