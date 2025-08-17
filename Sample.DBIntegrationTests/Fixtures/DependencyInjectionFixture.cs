using EcommerceDemo.Data.Data;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Implementations;
using Gator.DBDeltaCheck.Core.Implementations.Actions;
using Gator.DBDeltaCheck.Core.Implementations.Actions.DurableFunction;
using Gator.DBDeltaCheck.Core.Implementations.Assertions;
using Gator.DBDeltaCheck.Core.Implementations.Cleanup;
using Gator.DBDeltaCheck.Core.Implementations.Comparisons;
using Gator.DBDeltaCheck.Core.Implementations.Factories;
using Gator.DBDeltaCheck.Core.Implementations.Mapping;
using Gator.DBDeltaCheck.Core.Implementations.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;
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
        services.AddSingleton<ICleanupStrategyFactory, CleanupStrategyFactory>();
        services.AddSingleton<IDataComparisonRuleFactory, DataComparisonRuleFactory>();
        services.AddSingleton<IAssertionStrategyFactory, AssertionStrategyFactory>();
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

        // --- Assertion Strategies ---
        services.AddTransient<IAssertionStrategy, FlatAssertionStrategy>();
        services.AddTransient<IAssertionStrategy, HierarchicalAssertionStrategy>();

        // --- Comparison Rule ---
        services.AddTransient<IDataComparisonRule, IgnoreColumnsComparisonRule>();
        services.AddTransient<IDataComparisonRule, IgnoreOrderComparisonRule>();
        services.AddTransient<IDataComparisonRule, StrictEquivalenceComparisonRule>();

        // --- Seeding (Setup) Strategies ---
        services.AddTransient<ISetupStrategy, HierarchicalSeedingStrategy>();
        services.AddTransient<ISetupStrategy, JsonFileSeedingStrategy>();
    }

    private static void RegisterApplicationServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException(
                                   "ConnectionString 'DefaultConnection' not found.");

        services.AddDbContext<ECommerceDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ECommerceDbContext>());

        services.AddSingleton<IDbSchemaService, EFCachingDbSchemaService>();

        services.AddSingleton<IDatabaseRepository>(new DapperDatabaseRepository(connectionString));

        services.AddHttpClient();

        services.AddHttpClient("TestClient", client =>
        {
            // This URL should be in your appsettings.json
            var baseUrl = configuration.GetValue<string>("ApiBaseUrl");
            if (!string.IsNullOrEmpty(baseUrl)) client.BaseAddress = new Uri(baseUrl);
        });

        services.AddHttpClient<IDurableFunctionClient, DurableFunctionClient>(client =>
        {
            // This URL should be in your appsettings.json
            var baseUrl = configuration.GetValue<string>("DurableFunctionBaseUrl");
            if (!string.IsNullOrEmpty(baseUrl)) client.BaseAddress = new Uri(baseUrl);
        });


        services.AddSingleton(async sp =>
        {
            var dbRepository = sp.GetRequiredService<IDatabaseRepository>();
            // The connection is opened, used by Respawner.CreateAsync to build its internal model,
            // and then disposed. 
            await using var connection = dbRepository.GetDbConnection();
            await connection.OpenAsync();

            var schemaName = configuration["Respawner:SchemaName"] ?? "dbo";
            var tablesToIgnore = configuration.GetSection("Respawner:TablesToIgnore").Get<string[]>() ??
                                 [];

            var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                TablesToIgnore = tablesToIgnore.Select(t => new Table(t)).ToArray(),
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = [schemaName],
                WithReseed = true
            });

            return respawner;
        });

        services.AddTransient<IDataMapperValueResolver, DataMapperValueResolver>();
        services.AddTransient<ResolveToDbStrategy>();
        services.AddTransient<MapToFriendlyStrategy>();

        services.AddTransient<IDataMapper, DataMapper>();

    }

    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new TestAppSettings { Filename = "appsettings.json", IsOptional = false };
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return new ValueTask();
    }
}