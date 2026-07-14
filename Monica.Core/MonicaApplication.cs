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
    private readonly List<ILoggerFactory> _ownedCompositionLoggerFactories = [];
    private ILoggerFactory _compositionLoggerFactory;
    private bool _disposed;
    private TypeFinderOptions _typeFinderOptions = new();
    private ITypeFinder? _typeFinder;

    internal MonicaApplication()
    {
        _compositionLoggerFactory = CreateBootstrapLoggerFactory();
        _ownedCompositionLoggerFactories.Add(_compositionLoggerFactory);
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
        _typeFinderOptions,
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
        _typeFinderOptions = options;
        _typeFinder = null;
    }

    internal ILogger<T> CreateLogger<T>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _compositionLoggerFactory.CreateLogger<T>();
    }

    internal ILogger CreateLogger(Type categoryType)
    {
        ArgumentNullException.ThrowIfNull(categoryType);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _compositionLoggerFactory.CreateLogger(categoryType);
    }

    internal ILogger<T> CreateLogger<T>(LogLevel minimumLevel)
    {
        return new MinimumLevelLogger<T>(CreateLogger<T>(), minimumLevel);
    }

    internal void ReplaceCompositionLoggerFactory(ILoggerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (ReferenceEquals(_compositionLoggerFactory, factory))
        {
            return;
        }

        _compositionLoggerFactory = factory;
        if (!_ownedCompositionLoggerFactories.Any(ownedFactory =>
                ReferenceEquals(ownedFactory, factory)))
        {
            _ownedCompositionLoggerFactories.Add(factory);
        }
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        for (var index = _ownedCompositionLoggerFactories.Count - 1; index >= 0; index--)
        {
            _ownedCompositionLoggerFactories[index].Dispose();
        }

        _ownedCompositionLoggerFactories.Clear();
    }

    private static ILoggerFactory CreateBootstrapLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddConsole();
        });
    }
}
