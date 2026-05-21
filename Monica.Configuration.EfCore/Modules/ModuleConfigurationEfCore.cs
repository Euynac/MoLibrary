using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Stores;
using Monica.Configuration.Models;
using Monica.Repository;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for EF Core persistence in Monica.Configuration.
/// </summary>
public static class ModuleConfigurationEfCoreBuilderExtensions
{
    /// <summary>
    /// Registers EF Core as the Monica.Configuration distributed store bundle.
    /// </summary>
    /// <param name="guide">The configuration module guide.</param>
    /// <param name="optionsAction">DbContext configuration.</param>
    /// <returns>The configuration module guide.</returns>
    public static ModuleConfigurationGuide UseDbConfigurationStore(
        this ModuleConfigurationGuide guide,
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        new ModuleConfigurationEfCoreGuide().Register().UseDbContext(optionsAction);
        return guide;
    }
}

/// <summary>
/// EF Core module for Monica.Configuration.
/// </summary>
[ModuleKey("Monica.Configuration.EfCore")]
public sealed class ModuleConfigurationEfCore(ModuleConfigurationEfCoreOption option)
    : ModuleBase<ModuleConfigurationEfCore, ModuleConfigurationEfCoreOption, ModuleConfigurationEfCoreGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleConfigurationGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<DatabaseConfigurationStore>();
        services.Replace(ServiceDescriptor.Singleton<IConfigurationEffectiveValueStore>(
            serviceProvider => serviceProvider.GetRequiredService<DatabaseConfigurationStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationHistoryStore>(
            serviceProvider => serviceProvider.GetRequiredService<DatabaseConfigurationStore>()));
        services.Replace(ServiceDescriptor.Singleton<IConfigurationMetadataStore>(
            serviceProvider => serviceProvider.GetRequiredService<DatabaseConfigurationStore>()));
    }
}

/// <summary>
/// Fluent guide for EF Core configuration persistence.
/// </summary>
public sealed class ModuleConfigurationEfCoreGuide
    : ModuleGuide<ModuleConfigurationEfCore, ModuleConfigurationEfCoreOption, ModuleConfigurationEfCoreGuide>
{
    /// <inheritdoc />
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(UseDbContext)];
    }

    /// <summary>
    /// Registers the configuration DbContext.
    /// </summary>
    /// <param name="optionsAction">DbContext configuration.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationEfCoreGuide UseDbContext(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        DependsOnModule<ModuleRepositoryGuide>().Register()
            .AddRepositoryDbContext<ConfigurationDbContext>(optionsAction, DbContextProviderType.ContextFactory);
        ConfigureEmpty();
        return this;
    }
}

/// <summary>
/// Module options for EF Core configuration persistence.
/// </summary>
public sealed class ModuleConfigurationEfCoreOption : ModuleOptions<ModuleConfigurationEfCore>;
