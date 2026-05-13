using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Services;
using Monica.Configuration.EfCore.Sources;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for EF Core persistence in Monica.Configuration.
/// </summary>
public static class ModuleConfigurationEfCoreBuilderExtensions
{
    /// <summary>
    /// Registers EF Core as a Monica.Configuration value source and schema publisher.
    /// </summary>
    /// <param name="guide">The configuration module guide.</param>
    /// <param name="optionsAction">DbContext configuration.</param>
    /// <returns>The configuration module guide.</returns>
    public static ModuleConfigurationGuide UseEfCoreConfigurationStore(
        this ModuleConfigurationGuide guide,
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        new ModuleConfigurationEfCoreGuide().Register().UseDbContext(optionsAction);
        guide.AddValueSource<DatabaseConfigurationValueSource>();
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
        services.Replace(ServiceDescriptor.Scoped<IConfigurationDefinitionPublisher, EfCoreConfigurationDefinitionPublisher>());
        services.AddSingleton<IHostedService, ConfigurationDefinitionPublishingHostedService>();
    }
}

/// <summary>
/// Fluent guide for EF Core configuration persistence.
/// </summary>
public sealed class ModuleConfigurationEfCoreGuide
    : ModuleGuide<ModuleConfigurationEfCore, ModuleConfigurationEfCoreOption, ModuleConfigurationEfCoreGuide>
{
    /// <summary>
    /// Registers the configuration DbContext.
    /// </summary>
    /// <param name="optionsAction">DbContext configuration.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationEfCoreGuide UseDbContext(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        ConfigureServices(context =>
        {
            context.Services.AddDbContext<ConfigurationDbContext>(optionsAction);
        });
        return this;
    }
}

/// <summary>
/// Module options for EF Core configuration persistence.
/// </summary>
public sealed class ModuleConfigurationEfCoreOption : ModuleOptions<ModuleConfigurationEfCore>;
