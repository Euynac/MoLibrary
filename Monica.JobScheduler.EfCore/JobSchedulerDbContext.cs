using Microsoft.EntityFrameworkCore;
using Monica.DependencyInjection.Abstractions;
using Monica.JobScheduler.EfCore.Entities;
using Monica.Repository;
using Monica.Repository.Persistence.Services;

namespace Monica.JobScheduler.EfCore;

/// <summary>
/// DbContext for job scheduler metadata persistence.
/// </summary>
public class JobSchedulerDbContext(
    DbContextOptions<JobSchedulerDbContext> options,
    ICachedServiceProvider serviceProvider)
    : RepositoryDbContext<JobSchedulerDbContext>(options, serviceProvider)
{
    public DbSet<JobDefinitionEntity> JobDefinitions => Set<JobDefinitionEntity>();
    public DbSet<JobInstanceEntity> JobInstances => Set<JobInstanceEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
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
    }

  
}
