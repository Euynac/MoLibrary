using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.EfCore;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Extension methods for integrating EF Core persistence into JobScheduler module.
/// </summary>
public static class ModuleJobSchedulerEfCoreBuilderExtensions
{
    /// <summary>
    /// Configures the JobScheduler to use EF Core for metadata persistence.
    /// </summary>
    /// <param name="module">The JobScheduler registration that will include EF Core persistence.</param>
    /// <param name="optionsAction">Action to configure DbContext options (connection string, provider, etc.)</param>
    /// <param name="moduleAction">Optional action to configure JobSchedulerEfCore module options.</param>
    /// <returns>The JobScheduler module registration for method chaining.</returns>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseEfCoreMetadataRepository(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> module,
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction,
        Action<ModuleJobSchedulerEfCoreOption>? moduleAction = null)
    {
        module.Include<ModuleJobSchedulerEfCore, ModuleJobSchedulerEfCoreOption>(moduleAction)
            .UseDbContext(optionsAction);

        return module;
    }
}

/// <summary>
/// Fluent configuration builder for the Job Scheduler EF Core module.
/// </summary>
public static class ModuleJobSchedulerEfCoreRegistrationExtensions
{
    /// <summary>
    /// Configures the EF Core DbContext for job scheduler persistence.
    /// </summary>
    /// <param name="module">The JobScheduler EF Core registration being configured.</param>
    /// <param name="optionsAction">Action to configure DbContext options (connection string, provider, etc.)</param>
    /// <returns>The JobScheduler EF Core module registration for method chaining.</returns>
    public static ModuleRegistration<ModuleJobSchedulerEfCore, ModuleJobSchedulerEfCoreOption> UseDbContext(this ModuleRegistration<ModuleJobSchedulerEfCore, ModuleJobSchedulerEfCoreOption> module,
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        module.Require<ModuleRepository, ModuleRepositoryOption>()
            .AddRepositoryDbContext<JobSchedulerDbContext>(optionsAction);
        module.Require<ModuleJobScheduler, ModuleJobSchedulerOption>().UseCustomMetadataRepository<EfCoreJobMetadataRepository>();
        module.SatisfyFeature(nameof(UseDbContext));
        return module;
    }

}
/// <summary>
/// Module implementation for Job Scheduler EF Core persistence.
/// Provides EF Core-based storage for job definitions and instances.
/// </summary>
public class ModuleJobSchedulerEfCore : MonicaModule<ModuleJobSchedulerEfCoreOption>
{

    public override void ConfigureServices(ModuleContext<ModuleJobSchedulerEfCoreOption> context)
    {
        var services = context.Services;
        services.AddSingleton<IJobMetadataRepository, EfCoreJobMetadataRepository>();
    }

    public override void Describe(ModuleDescriptor module)
    {
        module.RequireFeature(nameof(ModuleJobSchedulerEfCoreRegistrationExtensions.UseDbContext));
    }
}
/// <summary>
/// Configuration options for the Job Scheduler EF Core module.
/// </summary>
public class ModuleJobSchedulerEfCoreOption : ModuleOptions<ModuleJobSchedulerEfCore>
{
    // Currently no specific options needed
    // Options for database configuration should be passed through AddRepositoryDbContext
}
