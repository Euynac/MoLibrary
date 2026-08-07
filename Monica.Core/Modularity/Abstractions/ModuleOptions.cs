namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Base class for one module's startup-frozen options and host-default accessors.
/// </summary>
/// <typeparam name="TModule">The module strategy that owns this option type.</typeparam>
public abstract class ModuleOptions<TModule> : IModuleOptions, IModuleOptionsContext where TModule : IModule
{
    private MonicaApplication? _application;

    /// <summary>
    /// Gets the application identity defaults owned by the current host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown before the option is bound to a Monica host.</exception>
    protected IMonicaApplicationOptions Application =>
        _application?.Application
        ?? throw new InvalidOperationException($"{GetType().Name} has not been bound to a Monica host.");

    /// <summary>
    /// Gets the module-system defaults owned by the current host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown before the option is bound to a Monica host.</exception>
    protected IMonicaModuleSystemOptions ModuleSystem =>
        _application?.ModuleSystem
        ?? throw new InvalidOperationException($"{GetType().Name} has not been bound to a Monica host.");

    /// <summary>
    /// Binds host-owned defaults before developer configuration is applied.
    /// </summary>
    /// <param name="application">The owning Monica application.</param>
    void IModuleOptionsContext.Bind(MonicaApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_application is not null && !ReferenceEquals(_application, application))
        {
            throw new InvalidOperationException($"{GetType().Name} is already bound to another Monica host.");
        }

        _application = application;
    }
}
