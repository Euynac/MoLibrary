using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
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
    : ModuleGuide<ModuleJobSchedulerEfCore, ModuleJobSchedulerEfCoreOption, ModuleJobSchedulerEfCoreGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(UseDbContext)];
    }

    /// <summary>
    /// Configures the EF Core DbContext for job scheduler persistence.
    /// </summary>
    /// <param name="optionsAction">Action to configure DbContext options (connection string, provider, etc.)</param>
    /// <returns>The guide for method chaining.</returns>
    public ModuleJobSchedulerEfCoreGuide UseDbContext(
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        // Register Repository module dependency with DbContext using ContextFactory provider
        // This allows Singleton services (like JobDefinitionCacheService) to safely use the repository
        DependsOnModule<ModuleRepositoryGuide>().Register()
            .AddRepositoryDbContext<JobSchedulerDbContext>(optionsAction, DbContextProviderType.ContextFactory);
        DependsOnModule<ModuleJobSchedulerGuide>().Register().UseCustomMetadataRepository<EfCoreJobMetadataRepository>();
        ConfigureEmpty();
        return this;
    }
}
/// <summary>
/// Module implementation for Job Scheduler EF Core persistence.
/// Provides EF Core-based storage for job definitions and instances.
/// </summary>
[ModuleKey(BuiltInModuleKey.JobSchedulerEfCore)]
public class ModuleJobSchedulerEfCore(ModuleJobSchedulerEfCoreOption option)
    : ModuleBase<ModuleJobSchedulerEfCore, ModuleJobSchedulerEfCoreOption, ModuleJobSchedulerEfCoreGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IJobMetadataRepository, EfCoreJobMetadataRepository>();
    }

    public override void ClaimDependencies()
    {
        
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
