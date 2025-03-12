using FluentMigrator.Runner;
using FluentMigrator.Runner.VersionTableInfo;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace UniverseLabs.Oms.Migrations;

public class MigratorRunner(string connectionString)
{
    public void Migrate()
    {
        var serviceProvider = CreateServices();

        using var scope = serviceProvider.CreateScope();
        UpdateDatabase(serviceProvider.GetRequiredService<IMigrationRunner>());
    }

    private IServiceProvider CreateServices()
    {
        Console.WriteLine(typeof(MigratorRunner).Assembly.FullName);
        return new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(MigratorRunner).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .AddScoped<IVersionTableMetaData, VersionTable>()
            .BuildServiceProvider(false);
    }

    private void UpdateDatabase(IMigrationRunner runner)
    {
        runner.MigrateUp();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        connection.ReloadTypes();
    }
}
