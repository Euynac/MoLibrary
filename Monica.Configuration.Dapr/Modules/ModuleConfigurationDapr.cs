using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Dapr.Notifications;
using Monica.Configuration.Dapr.Sources;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for Dapr integration in Monica.Configuration.
/// </summary>
public static class ModuleConfigurationDaprBuilderExtensions
{
    /// <summary>
    /// Registers Dapr Configuration as a read-only value source.
    /// </summary>
    /// <param name="guide">The configuration guide.</param>
    /// <returns>The configuration guide.</returns>
    public static ModuleConfigurationGuide UseDaprConfigurationSource(this ModuleConfigurationGuide guide)
    {
        new ModuleConfigurationDaprGuide().Register();
        guide.AddValueSource<DaprConfigurationValueSource>();
        return guide;
    }

    /// <summary>
    /// Registers Dapr pub/sub as a change notification transport.
    /// </summary>
    /// <param name="guide">The configuration guide.</param>
    /// <returns>The configuration guide.</returns>
    public static ModuleConfigurationGuide UseDaprChangeNotifications(this ModuleConfigurationGuide guide)
    {
        new ModuleConfigurationDaprGuide().Register();
        return guide;
    }
}

/// <summary>
/// Dapr provider module for Monica.Configuration.
/// </summary>
[ModuleKey("Monica.Configuration.Dapr")]
public sealed class ModuleConfigurationDapr(ModuleConfigurationDaprOption option)
    : ModuleBase<ModuleConfigurationDapr, ModuleConfigurationDaprOption, ModuleConfigurationDaprGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleConfigurationGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfigurationChangeBroadcaster, DaprConfigurationChangeBroadcaster>();
        services.AddSingleton<IConfigurationChangeSubscriber, DaprConfigurationChangeSubscriber>();
    }
}

/// <summary>
/// Fluent guide for Dapr configuration integration.
/// </summary>
public sealed class ModuleConfigurationDaprGuide
    : ModuleGuide<ModuleConfigurationDapr, ModuleConfigurationDaprOption, ModuleConfigurationDaprGuide>;

/// <summary>
/// Module options for Dapr configuration integration.
/// </summary>
public sealed class ModuleConfigurationDaprOption : ModuleOptions<ModuleConfigurationDapr>;
