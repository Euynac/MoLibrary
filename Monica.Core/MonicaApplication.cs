using Monica.Core.Modularity.State;
using Monica.Core.TypeDiscovery.Abstractions;
using Monica.Core.TypeDiscovery.Models;
using Monica.Core.TypeDiscovery.Services;

namespace Monica.Core;

/// <summary>
/// Owns the mutable Monica application state used by module registration and diagnostics.
/// </summary>
/// <remarks>
/// Production code normally uses the process-default instance through <see cref="Mo"/>.
/// Tests can create scoped instances to isolate module registration state without resetting process-global fields.
/// </remarks>
public sealed class MonicaApplication : IDisposable
{
    private static readonly MonicaApplication PROCESS_DEFAULT = new();
    private static readonly AsyncLocal<MonicaApplication?> CURRENT = new();

    private readonly MonicaApplication? _previous;
    private bool _disposed;
    private ITypeFinder? _typeFinder;

    private MonicaApplication(MonicaApplication? previous = null)
    {
        _previous = previous;
    }

    /// <summary>
    /// Gets the Monica application bound to the current async flow, or the process-default application when no scope exists.
    /// </summary>
    public static MonicaApplication Current => CURRENT.Value ?? PROCESS_DEFAULT;

    /// <summary>
    /// Gets shared Monica application identity defaults for this application instance.
    /// </summary>
    public MonicaApplicationOptions Application { get; } = new();

    /// <summary>
    /// Gets shared Monica module-system defaults for this application instance.
    /// </summary>
    public MonicaModuleSystemOptions ModuleSystem { get; } = new();

    /// <summary>
    /// Gets module registration state for this application instance.
    /// </summary>
    internal ModuleRegistryState Registry { get; } = new();

    /// <summary>
    /// Gets module dependency state for this application instance.
    /// </summary>
    internal ModuleDependencyState Dependencies { get; } = new();

    /// <summary>
    /// Gets module initialization profiling state for this application instance.
    /// </summary>
    internal ModuleProfilingState Profiling { get; } = new();

    /// <summary>
    /// Gets module enablement state for this application instance.
    /// </summary>
    internal ModuleState State { get; } = new();

    /// <summary>
    /// Gets the domain type finder used by this Monica application instance.
    /// </summary>
    public ITypeFinder TypeFinder => _typeFinder ??= new DomainTypeFinder(new TypeFinderOptions());

    /// <summary>
    /// Creates a new Monica application scope for the current async flow.
    /// </summary>
    /// <returns>The scoped application. Dispose it to restore the previous application.</returns>
    public static MonicaApplication CreateScoped()
    {
        var scoped = new MonicaApplication(CURRENT.Value);
        CURRENT.Value = scoped;
        return scoped;
    }

    /// <summary>
    /// Activates this Monica application for the current async flow until the returned scope is disposed.
    /// </summary>
    /// <returns>A scope that restores the previous ambient application on disposal.</returns>
    public IDisposable Activate()
    {
        var previous = CURRENT.Value;
        CURRENT.Value = this;
        return new ActivationScope(previous);
    }

    /// <summary>
    /// Rebuilds the domain type finder with an optional configuration callback.
    /// </summary>
    /// <param name="configure">An optional callback that customizes type discovery before creation.</param>
    public void ConfigureTypeDiscovery(Action<TypeFinderOptions>? configure = null)
    {
        var options = new TypeFinderOptions();
        configure?.Invoke(options);
        _typeFinder = new DomainTypeFinder(options);
    }

    /// <summary>
    /// Clears module lifecycle state while preserving application-level defaults.
    /// </summary>
    public void ResetModuleState()
    {
        Registry.Clear();
        Dependencies.Clear();
        Profiling.Clear();
        State.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (CURRENT.Value == this)
        {
            CURRENT.Value = _previous;
        }

        _disposed = true;
    }

    private sealed class ActivationScope(MonicaApplication? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CURRENT.Value = previous;
            _disposed = true;
        }
    }
}
