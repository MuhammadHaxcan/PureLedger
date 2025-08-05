using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;

namespace PureLedger.Data
{
    public class SubrouteDbContextFactory
    {
        private readonly IConfiguration _configuration;

        public SubrouteDbContextFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public LedgerDbContext CreateDbContext(string subrouteName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<LedgerDbContext>();
            string connectionString;

            // Check if we have a valid subroute name and if it exists in config
            if (!string.IsNullOrEmpty(subrouteName) && subrouteName != "Default")
            {
                // Try to get the connection string for the specific subroute
                connectionString = _configuration.GetSection($"Subroutes:{subrouteName}:ConnectionString").Value;

                // If found, log which connection is being used
                if (!string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine($"Using connection string for subroute: {subrouteName}");
                }
                else
                {
                    // Fallback to default if not found
                    connectionString = _configuration.GetConnectionString("LedgerDb");
                    Console.WriteLine($"Subroute {subrouteName} configuration not found, using default connection");
                }
            }
            else
            {
                // Use default connection if no subroute specified
                connectionString = _configuration.GetConnectionString("LedgerDb");
                Console.WriteLine("Using default connection string");
            }

            optionsBuilder.UseSqlServer(connectionString);
            return new LedgerDbContext(optionsBuilder.Options);
        }
    }
}