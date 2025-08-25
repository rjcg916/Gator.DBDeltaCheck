
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Implementations;
using Gator.DBDeltaCheck.Core.Implementations.Commands;
using Gator.DBDeltaCheck.Core.Implementations.Mapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using STARFake.EF.Models;

namespace Gator.DBDeltaCheck.Tools;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Build the host to set up all services.
        var host = BuildHost();

        // Create an instance of command handler, giving it the host.
        var handler = new CommandLineHandler(host);

        // Delegate all command-line processing to the handler.
        return await handler.RunCommandAsync(args);
    }

    /// <summary>
    /// Configures and builds the dependency injection host.
    /// </summary>
    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                services.AddDbContext<STARContext>(options => options.UseSqlServer(connectionString));
                services.AddScoped<DbContext>(sp => sp.GetRequiredService<STARContext>());
                services.AddTransient<IDatabaseRepository, DapperDatabaseRepository>(sp => new DapperDatabaseRepository(connectionString));
                services.AddSingleton<IDbSchemaService, EfCachingDbSchemaService>();
                services.AddTransient<IDataMapperValueResolver, DataMapperValueResolver>();
                services.AddTransient<ResolveToDbStrategy>();
                services.AddTransient<MapToFriendlyStrategy>();
                services.AddTransient<IDataMapper, DataMapper>();
                services.AddTransient<HierarchyScaffolder>();
                services.AddTransient<HierarchyTemplateGenerator>();
            })
            .Build();
}