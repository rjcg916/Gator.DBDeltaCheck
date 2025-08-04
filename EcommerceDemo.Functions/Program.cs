using ECommerceDemo.Data.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Get the connection string from the configuration (local.settings.json)
        var connectionString = Environment.GetEnvironmentVariable("DefaultConnection")
                               ?? throw new InvalidOperationException(
                                   "Connection string 'DefaultConnection' not found.");

        // Register the ECommerceDbContext
        services.AddDbContext<ECommerceDbContext>(options =>
            options.UseSqlServer(connectionString));
    })
    .Build();

host.Run();