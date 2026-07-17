using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.EfCore.DbContext;
using Monica.DependencyInjection.Services;
using Monica.Modules;

namespace Test.Monica.Configuration.EfCore;

/// <summary>
/// Creates the SQLite configuration context used to scaffold test-owned migrations.
/// </summary>
public sealed class ConfigurationStoreTestContextFactory : IDesignTimeDbContextFactory<ConfigurationDbContext>
{
    internal const string MigrationsHistoryTable = "__EFMigrationsHistory_ConfigurationTests";

    /// <inheritdoc />
    public ConfigurationDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<ModuleRepositoryOption>(static _ => { });
        var serviceProvider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>();
        ConfigureOptions(options, "Data Source=:memory:");
        return new ConfigurationDbContext(options.Options, new CachedServiceProvider(serviceProvider));
    }

    internal static void ConfigureOptions(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlite(
            connectionString,
            sqliteOptions =>
            {
                sqliteOptions.MigrationsAssembly(
                    typeof(ConfigurationStoreTestContextFactory).Assembly.GetName().Name!);
                sqliteOptions.MigrationsHistoryTable(MigrationsHistoryTable);
            });
    }
}
