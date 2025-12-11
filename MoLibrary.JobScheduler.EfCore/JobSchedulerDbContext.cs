using Microsoft.EntityFrameworkCore;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.JobScheduler.EfCore.Entities;
using MoLibrary.Repository;

namespace MoLibrary.JobScheduler.EfCore;

/// <summary>
/// DbContext for job scheduler metadata persistence.
/// </summary>
public class JobSchedulerDbContext(
    DbContextOptions<JobSchedulerDbContext> options,
    IMoServiceProvider serviceProvider)
    : MoDbContext<JobSchedulerDbContext>(options, serviceProvider)
{
    public DbSet<JobDefinitionEntity> JobDefinitions => Set<JobDefinitionEntity>();
    public DbSet<JobInstanceEntity> JobInstances => Set<JobInstanceEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HavePrecision(0);
        // Configure timestamp with time zone for PostgreSQL/GaussDB
        var providerName = Database.ProviderName;
        if (providerName != null && (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                                     providerName.Contains("GaussDB", StringComparison.OrdinalIgnoreCase)))
        {
            configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp with time zone");
        }
        else
        {
            configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp");
        }
        configurationBuilder.Properties<TimeOnly>().HavePrecision(0);
        configurationBuilder.Properties<TimeSpan>().HavePrecision(0);
    }

  
}
