using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core.Modularity.Services;

/// <summary>
/// Default host-bound Monica composition builder.
/// </summary>
internal sealed class MonicaBuilder(IHostApplicationBuilder hostBuilder, MonicaApplication application)
    : IMonicaBuilder
{
    private bool _isCompleted;

    /// <inheritdoc />
    public IMonicaBuilder ConfigureApplication(Action<MonicaApplicationOptions> configure)
    {
        EnsureCompositionIsOpen();
        ArgumentNullException.ThrowIfNull(configure);
        configure(application.Application);
        return this;
    }

    /// <inheritdoc />
    public IMonicaBuilder ConfigureModuleSystem(Action<MonicaModuleSystemOptions> configure)
    {
        EnsureCompositionIsOpen();
        ArgumentNullException.ThrowIfNull(configure);
        configure(application.ModuleSystem);
        return this;
    }

    /// <inheritdoc />
    public IMonicaBuilder ConfigureTypeDiscovery(Action<TypeFinderOptions>? configure = null)
    {
        EnsureCompositionIsOpen();
        application.ConfigureTypeDiscovery(configure);
        return this;
    }

    /// <inheritdoc />
    public TModuleGuide AddModule<TModule, TModuleOption, TModuleGuide>(
        Action<TModuleOption>? configure = null)
        where TModuleOption : ModuleOptions<TModule>, new()
        where TModuleGuide : ModuleGuide<TModule, TModuleOption, TModuleGuide>, new()
        where TModule : ModuleBase<TModule, TModuleOption, TModuleGuide>
    {
        EnsureCompositionIsOpen();
        return application.CreateGuide<TModuleGuide>().Register(configure);
    }

    /// <summary>
    /// Closes graph composition and applies the validated module lifecycle to the host.
    /// </summary>
    internal void Complete()
    {
        EnsureCompositionIsOpen();
        _isCompleted = true;

        application.ModuleSystem.Validate();
        application.Modules.RegisterServices(hostBuilder);
    }

    private void EnsureCompositionIsOpen()
    {
        if (_isCompleted)
        {
            throw new InvalidOperationException(
                "The Monica composition callback has completed and can no longer be modified.");
        }
    }
}
