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
}
