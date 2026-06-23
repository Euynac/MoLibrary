using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EventBus.Services;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Modules;

namespace Monica.Configuration.EventBus.Modules;

/// <summary>
/// Bridges Monica.Configuration change notifications to the distributed EventBus.
/// </summary>
[ModuleKey(BuiltInModuleKey.ConfigurationEventBus)]
public sealed class ModuleConfigurationEventBus(ModuleConfigurationEventBusOption option)
    : ModuleBase<ModuleConfigurationEventBus, ModuleConfigurationEventBusOption, ModuleConfigurationEventBusGuide>(option)
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigurationChangeNotifier, ConfigurationEventBusChangeNotifier>());
        services.AddHostedService<ConfigurationEventBusSubscriptionHostedService>();
    }

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleConfigurationGuide>().Register();
        DependsOnModule<ModuleEventBusGuide>().Register();
    }
}

/// <summary>
/// Configuration guide for the Monica.Configuration EventBus bridge module.
/// </summary>
public sealed class ModuleConfigurationEventBusGuide
    : ModuleGuide<ModuleConfigurationEventBus, ModuleConfigurationEventBusOption, ModuleConfigurationEventBusGuide>;

/// <summary>
/// Options for the Monica.Configuration EventBus bridge module.
/// </summary>
public sealed class ModuleConfigurationEventBusOption : ModuleOptions<ModuleConfigurationEventBus>
{
    /// <summary>
    /// Gets or sets the distributed EventBus topic used for configuration reload signals.
    /// </summary>
    /// <remarks>
    /// The message payload contains only invalidation metadata, never configuration values.
    /// All participating services must use the same topic name.
    /// </remarks>
    public string TopicName { get; set; } = "monica.configuration.reload";
}
