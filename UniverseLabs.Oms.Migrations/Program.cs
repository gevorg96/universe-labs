using Microsoft.Extensions.Configuration;

namespace UniverseLabs.Oms.Migrations;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains("--dryrun"))
        {
            return;
        }

        MigrateDatabase();
    }

    private static void MigrateDatabase()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                              throw new InvalidOperationException("ASPNETCORE_ENVIRONMENT in not set");

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile($"appsettings.{environmentName}.json")
            .Build();

        var connectionString = config["DbSettings:MigrationConnectionString"];
        var migrationRunner = new MigratorRunner(connectionString);
        migrationRunner.Migrate();
    }
}