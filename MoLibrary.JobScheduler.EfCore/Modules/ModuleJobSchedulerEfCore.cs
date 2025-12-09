using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.Repository.Modules;

namespace MoLibrary.JobScheduler.EfCore.Modules;

/// <summary>
/// Extension methods for integrating EF Core persistence into JobScheduler module.
/// </summary>
public static class ModuleJobSchedulerEfCoreBuilderExtensions
{
    /// <summary>
    /// Configures the JobScheduler to use EF Core for metadata persistence.
    /// </summary>
    /// <param name="guide">The JobScheduler module guide.</param>
    /// <param name="optionsAction">Action to configure DbContext options (connection string, provider, etc.)</param>
    /// <param name="moduleAction">Optional action to configure JobSchedulerEfCore module options.</param>
    /// <returns>The JobScheduler guide for method chaining.</returns>
    public static ModuleJobSchedulerGuide UseEfCoreMetadataRepository(
        this ModuleJobSchedulerGuide guide,
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction,
        Action<ModuleJobSchedulerEfCoreOption>? moduleAction = null)
    {
        new ModuleJobSchedulerEfCoreGuide().Register(moduleAction)
            .UseDbContext(optionsAction);

        return guide;
    }
}

/// <summary>
/// Fluent configuration builder for the Job Scheduler EF Core module.
/// </summary>
public class ModuleJobSchedulerEfCoreGuide
    : MoModuleGuide<ModuleJobSchedulerEfCore, ModuleJobSchedulerEfCoreOption, ModuleJobSchedulerEfCoreGuide>
{
    private const string CONFIG_DB_CONTEXT = nameof(CONFIG_DB_CONTEXT);

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [CONFIG_DB_CONTEXT];
    }

    /// <summary>
    /// Configures the EF Core DbContext for job scheduler persistence.
    /// </summary>
    /// <param name="optionsAction">Action to configure DbContext options (connection string, provider, etc.)</param>
    /// <returns>The guide for method chaining.</returns>
    public ModuleJobSchedulerEfCoreGuide UseDbContext(
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        // Register Repository module dependency with DbContext using Default provider
        DependsOnModule<ModuleRepositoryGuide>().Register()
            .AddMoDbContext<JobSchedulerDbContext>(optionsAction);

        // Register the EF Core repository implementation
        PostConfigureServices(context =>
        {
            // Register as singleton to match IMoJobMetadataRepository expectation
            // Thread safety is handled through scoped IDbContextProvider
            context.Services.AddSingleton<IMoJobMetadataRepository, EfCoreJobMetadataRepository>();
        }, key: CONFIG_DB_CONTEXT);

        return this;
    }
}
/// <summary>
/// Module implementation for Job Scheduler EF Core persistence.
/// Provides EF Core-based storage for job definitions and instances.
/// </summary>
public class ModuleJobSchedulerEfCore(ModuleJobSchedulerEfCoreOption option)
    : MoModuleWithDependencies<ModuleJobSchedulerEfCore, ModuleJobSchedulerEfCoreOption, ModuleJobSchedulerEfCoreGuide>(option)
{
    public override EMoModules CurModuleEnum() => EMoModules.JobSchedulerEfCore;

    public override void ClaimDependencies()
    {
        // Dependencies are configured in the guide
    }
}
/// <summary>
/// Configuration options for the Job Scheduler EF Core module.
/// </summary>
public class ModuleJobSchedulerEfCoreOption : MoModuleOption<ModuleJobSchedulerEfCore>
{
    // Currently no specific options needed
    // Options for database configuration should be passed through AddMoDbContext
}
