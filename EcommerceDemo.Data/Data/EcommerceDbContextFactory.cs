using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ECommerceDemo.Data
{
    /// <summary>
    /// This factory is used by the EF Core design-time tools (e.g., for Add-Migration)
    /// to create an instance of the DbContext. It provides a way to configure the
    /// DbContext with a connection string from appsettings.json at design time.
    /// </summary>
    public class ECommerceDbContextFactory : IDesignTimeDbContextFactory<ECommerceDbContext>
    {
        public ECommerceDbContext CreateDbContext(string[] args)
        {
            // This logic builds a configuration object that reads from appsettings.json.
            // It looks for the file in the directory of the startup project.
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ECommerceDbContext>();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("The connection string 'DefaultConnection' was not found in appsettings.json.");
            }

            optionsBuilder.UseSqlServer(connectionString);

            return new ECommerceDbContext(optionsBuilder.Options);
        }
    }
}