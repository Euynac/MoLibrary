using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;
using Monica.Core.TypeDiscovery.Abstractions;
using Monica.Core.TypeDiscovery.Models;
using Monica.Core.TypeDiscovery.Services;

namespace Monica.Core;

/// <summary>
/// Owns the configuration, module graph, lifecycle state, and diagnostics for one Monica host.
/// </summary>
/// <remarks>
/// The host creates this context through <c>AddMonica</c> and registers the same instance in dependency injection.
/// Monica does not use process-global or async-flow ambient registration state.
/// </remarks>
public sealed class MonicaApplication : IDisposable
{
    private readonly RegistrationLoggerFactory _registrationLoggerFactory = new();
    private ITypeFinder? _typeFinder;

    internal MonicaApplication()
    {
        Dependencies = new ModuleDependencyAnalyzer(this);
        Profiling = new ModuleInitializationProfiler();
        ModuleStates = new ModuleStateRegistry(this);
        Modules = new ModuleRegistry(this);
        Errors = new ModuleErrorRegistry(this);
    }

    /// <summary>
    /// Gets application identity defaults for this host.
    /// </summary>
    public MonicaApplicationOptions Application { get; } = new();

    /// <summary>
    /// Gets module-system defaults for this host.
    /// </summary>
    public MonicaModuleSystemOptions ModuleSystem { get; } = new();

    /// <summary>
    /// Gets the business-type finder configured for this host.
    /// </summary>
    public ITypeFinder TypeFinder => _typeFinder ??= new DomainTypeFinder(
        new TypeFinderOptions(),
        CreateLogger<DomainTypeFinder>());

    /// <summary>
    /// Gets the host-bound module registry and its read-only runtime views.
    /// </summary>
    public ModuleRegistry Modules { get; }

    /// <summary>
    /// Gets the host-bound module dependency analyzer.
    /// </summary>
    public ModuleDependencyAnalyzer Dependencies { get; }

    internal ModuleInitializationProfiler Profiling { get; }

    internal ModuleStateRegistry ModuleStates { get; }

    internal ModuleErrorRegistry Errors { get; }

    /// <summary>
    /// Rebuilds business-type discovery for this host.
    /// </summary>
    /// <param name="configure">An optional callback that customizes discovery.</param>
    public void ConfigureTypeDiscovery(Action<TypeFinderOptions>? configure = null)
    {
        var options = new TypeFinderOptions();
        configure?.Invoke(options);
        _typeFinder = new DomainTypeFinder(options, CreateLogger<DomainTypeFinder>());
    }

    internal ILogger<T> CreateLogger<T>()
    {
        return _registrationLoggerFactory.CreateLogger<T>();
    }

    internal ILogger CreateLogger(Type categoryType)
    {
        ArgumentNullException.ThrowIfNull(categoryType);
        return _registrationLoggerFactory.CreateLogger(categoryType);
    }

    internal ILogger<T> CreateLogger<T>(LogLevel minimumLevel)
    {
        return new MinimumLevelLogger<T>(CreateLogger<T>(), minimumLevel);
    }

    internal void ReplaceRegistrationLoggerFactory(ILoggerFactory factory, bool ownsFactory)
    {
        _registrationLoggerFactory.Replace(factory, ownsFactory);
    }

    internal TGuide CreateGuide<TGuide>(ModuleKey? configuredBy = null)
        where TGuide : ModuleGuide, new()
    {
        var guide = new TGuide();
        guide.Bind(this, configuredBy);
        return guide;
    }

    internal void ResetModuleState()
    {
        Modules.Clear();
        Dependencies.Clear();
        Profiling.Clear();
        ModuleStates.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _registrationLoggerFactory.Dispose();
    }
}
