﻿using DBDeltaCheck.Core.ComparisonStrategies;
using DBDeltaCheck.Core.Implementations;
using DBDeltaCheck.Core.Implementations.Comparisons;
using ECommerceDemo.Data;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Implementations;
using Gator.DBDeltaCheck.Core.Implementations.Actions;
using Gator.DBDeltaCheck.Core.Implementations.Cleanup;
using Gator.DBDeltaCheck.Core.Implementations.Comparisons;
using Gator.DBDeltaCheck.Core.Implementations.Factories;
using Gator.DBDeltaCheck.Core.Implementations.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using System.Data.Common;
using Xunit.Microsoft.DependencyInjection;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace Sample.DBIntegrationTests.Fixtures;

public class DependencyInjectionFixture : TestBedFixture
{

    protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddTransient<HttpDurableFunctionClient>();

        RegisterFactories(services);
        RegisterStrategies(services); // This method has the most important changes.
        RegisterApplicationServices(services, configuration);
    }

    private static void RegisterFactories(IServiceCollection services)
    {
        services.AddSingleton<IActionStrategyFactory, ActionStrategyFactory>();
        services.AddSingleton<ICleanupStrategyFactory, CleanupStrategyFactory>();
        services.AddSingleton<IComparisonStrategyFactory, ComparisonStrategyFactory>();
        services.AddSingleton<ISetupStrategyFactory, SetupStrategyFactory>();
    }

    private static void RegisterStrategies(IServiceCollection services)
    {


        // --- Action Strategies ---
        services.AddTransient<IActionStrategy, ApiCallActionStrategy>();
        services.AddTransient<IActionStrategy, DurableFunctionActionStrategy>();
 
        // --- Cleanup Strategies ---
        services.AddTransient<ICleanupStrategy, DeleteFromTableStrategy>();
        services.AddTransient<ICleanupStrategy, RespawnCleanupStrategy>();

        // --- Comparison Strategies ---
        services.AddTransient<IComparisonStrategy, IgnoreColumnsComparisonStrategy>();
        services.AddTransient<IComparisonStrategy, IgnoreOrderComparisonStrategy>();
        services.AddTransient<IComparisonStrategy, StrictEquivalenceComparisonStrategy>();

        // --- Seeding (Setup) Strategies ---
        services.AddTransient<ISetupStrategy, HierarchicalSeedingStrategy>();
        services.AddTransient<ISetupStrategy, JsonFileSeedingStrategy>();
    }

    private static void RegisterApplicationServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' not found.");

  
        services.AddDbContext<ECommerceDbContext>(options =>
            options.UseSqlServer(connectionString));


        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ECommerceDbContext>());


        services.AddSingleton<IDbSchemaService,  EFCachingDbSchemaService>();


        services.AddSingleton<IDatabaseRepository>(new DapperDatabaseRepository(connectionString));

        // This HttpClient registration is correct.
        services.AddHttpClient<HttpDurableFunctionClient>(client =>
        {
            var baseUrl = configuration.GetValue<string>("DurableFunctionBaseUrl");
            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        });


        services.AddSingleton(async sp =>
        {
            var dbRepository = sp.GetRequiredService<IDatabaseRepository>();
            // The connection is opened, used by Respawner.CreateAsync to build its internal model,
            // and then disposed. 
            using var connection = dbRepository.GetDbConnection();
            await connection.OpenAsync();

            var tablesToIgnore = configuration.GetSection("Respawn:TablesToIgnore").Get<string[]>() ?? Array.Empty<string>();

            var respawner = await Respawner.CreateAsync((DbConnection)connection, new RespawnerOptions
            {
                TablesToIgnore = tablesToIgnore.Select(t => new Respawn.Graph.Table(t)).ToArray(),
                DbAdapter = DbAdapter.SqlServer
            });

            return respawner;
        });
    }


    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new() { Filename = "appsettings.json", IsOptional = false };
    }

    protected override ValueTask DisposeAsyncCore() => new();
}
