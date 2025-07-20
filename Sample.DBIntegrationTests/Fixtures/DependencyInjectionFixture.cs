using DB.IntegrationTests.Tests;
using DBDeltaCheck.Core.Abstractions.Factories;
using DBDeltaCheck.Core.ComparisonStrategies;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Implementations;
using Gator.DBDeltaCheck.Core.Implementations.Actions;
using Gator.DBDeltaCheck.Core.Implementations.Cleanup;
using Gator.DBDeltaCheck.Core.Implementations.Comparisons;
using Gator.DBDeltaCheck.Core.Implementations.Factories;
using Gator.DBDeltaCheck.Core.Implementations.Seeding;
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

        RegisterFactories(services);
        RegisterStrategies(services);
        RegisterApplicationServices(services, configuration);
    }

    private static void RegisterFactories(IServiceCollection services)
    {
        services.AddSingleton<IActionStrategyFactory, ActionStrategyFactory>();
        services.AddSingleton<ISetupStrategyFactory, SetupStrategyFactory>();
        services.AddSingleton<ICleanupStrategyFactory, CleanupStrategyFactory>();
        services.AddSingleton<IComparisonStrategyFactory, ComparisonStrategyFactory>();
    }

    private static void RegisterStrategies(IServiceCollection services)
    {
        services.AddTransient<DurableFunctionActionStrategy>();
        services.AddTransient<ApiCallActionStrategy>();

        services.AddTransient<JsonFileSeedingStrategy>();

        services.AddTransient<DeleteFromTableStrategy>();
        services.AddTransient<RespawnCleanupStrategy>();

        services.AddTransient<StrictEquivalenceStrategy>();
        services.AddTransient<IgnoreColumnsStrategy>();
        services.AddTransient<IgnoreOrderStrategy>();
    }

    private static void RegisterApplicationServices(IServiceCollection services, IConfiguration configuration)
    {

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddSingleton<IDatabaseOperations>(new DapperDatabaseRepository());

        services.AddHttpClient<HttpDurableFunctionClient>(client =>
        {
            client.BaseAddress = new Uri(configuration.GetValue<string>("DurableFunctionBaseUrl", "").ToLower());
        });

        services.AddSingleton((Func<IServiceProvider, Task<Respawner>>)(async sp =>
        {

            var dbRepository = sp.GetRequiredService<IDatabaseRepository>();

            using var connection = dbRepository.GetDbConnection();

            connection.Open();

            var tablesToIgnoreFromConfig = configuration.GetSection("Respawn:TablesToIgnore").Get<string[]>() ?? Array.Empty<string>();

            var respawner = await Respawner.CreateAsync((DbConnection)connection, new RespawnerOptions
            {
                TablesToIgnore = tablesToIgnoreFromConfig.Select(t => new Respawn.Graph.Table(t)).ToArray(),
                DbAdapter = DbAdapter.SqlServer
            });

            return respawner;
        }));
    }

    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new() { Filename = "appsettings.json", IsOptional = false };
    }

    protected override ValueTask DisposeAsyncCore() => new();
}
