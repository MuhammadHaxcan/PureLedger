using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PureLedger.Data
{
    public class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
    {
        public LedgerDbContext CreateDbContext(string[] args)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var backendDirectory = Path.Combine(currentDirectory, "Pureledger-Backend");
            var basePath = Directory.Exists(backendDirectory) ? backendDirectory : currentDirectory;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets<LedgerDbContextFactory>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<LedgerDbContext>();
            optionsBuilder.UseNpgsql(configuration.GetConnectionString("LedgerDb"));

            return new LedgerDbContext(optionsBuilder.Options);
        }
    }
}
