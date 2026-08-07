using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Monica.Core.Modularity.State;

/// <summary>
/// Identifies the host-owned boundary that completed Monica composition.
/// </summary>
internal enum ModuleCompositionCompletionPoint
{
    ServiceRegistration,
    EndpointMapping
}

/// <summary>Identifies a bounded structural cause for terminal composition failure.</summary>
internal enum ModuleCompositionFailureKind
{
    /// <summary>Service registration or final composition validation failed.</summary>
    ServiceRegistration,

    /// <summary>Application-pipeline configuration failed.</summary>
    ApplicationPipeline,

    /// <summary>Endpoint mapping failed.</summary>
    EndpointMapping,

    /// <summary>Host startup validation found incomplete Web composition.</summary>
    StartupValidation,

    /// <summary>Final successful-completion publication failed.</summary>
    Completion
}

/// <summary>
/// Owns the composition transitions for one Monica host.
/// </summary>
internal sealed class ModuleCompositionState
{
    private readonly object _gate = new();
    private IApplicationBuilder? _applicationBuilder;
    private ModuleCompositionCompletionPoint? _completionPoint;
    private Exception? _completionFailure;
    private ModuleCompositionFailureKind? _failureKind;
    private bool _completionAttempted;
    private bool _mapMonicaInvoked;
    private ModuleCompositionCompletionPoint? _pendingCompletionPoint;
    private bool _requiresEndpointMapping;
    private bool _useMonicaInvoked;
    private long _revision;

    /// <summary>
    /// Gets the concrete host-builder type used by the composition.
    /// </summary>
    internal Type? HostBuilderType { get; private set; }

    /// <summary>
    /// Starts composition tracking for the supplied host builder.
    /// </summary>
    internal void Initialize(IHostApplicationBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        lock (_gate)
        {
            if (HostBuilderType is not null)
            {
                throw new InvalidOperationException("Monica composition tracking has already been initialized.");
            }

            HostBuilderType = hostBuilder.GetType();
            _requiresEndpointMapping = hostBuilder is WebApplicationBuilder;
            _revision++;
        }
    }

    /// <summary>
    /// Claims the Web application builder for the single <c>UseMonica()</c> invocation.
    /// </summary>
    internal void BeginApplicationPipeline(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        lock (_gate)
        {
            EnsureWebComposition();

            if (_applicationBuilder is not null && !ReferenceEquals(_applicationBuilder, app))
            {
                throw new InvalidOperationException(
                    "UseMonica() and MapMonica() must be called on the same application builder instance.");
            }

            if (_useMonicaInvoked)
            {
                throw new InvalidOperationException("UseMonica() can be called only once for a Monica Web host.");
            }

            EnsureCompositionIsPending();

            _applicationBuilder = app;
            _useMonicaInvoked = true;
            _revision++;
        }
    }

    /// <summary>
    /// Claims the Web application builder for the single <c>MapMonica()</c> invocation.
    /// </summary>
    internal void BeginEndpointMapping(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        lock (_gate)
        {
            EnsureWebComposition();

            if (_applicationBuilder is not null && !ReferenceEquals(_applicationBuilder, app))
            {
                throw new InvalidOperationException(
                    "UseMonica() and MapMonica() must be called on the same application builder instance.");
            }

            if (_mapMonicaInvoked)
            {
                throw new InvalidOperationException("MapMonica() can be called only once for a Monica Web host.");
            }

            EnsureCompositionIsPending();

            if (!_useMonicaInvoked)
            {
                throw new InvalidOperationException("Call UseMonica() before MapMonica().");
            }

            _mapMonicaInvoked = true;
            _revision++;
        }
    }

    /// <summary>
    /// Completes composition at the host-appropriate boundary.
    /// </summary>
    /// <returns><see langword="true"/> only for the first successful completion.</returns>
    internal bool TryBeginCompletion(ModuleCompositionCompletionPoint completionPoint)
    {
        lock (_gate)
        {
            var expectedPoint = _requiresEndpointMapping
                ? ModuleCompositionCompletionPoint.EndpointMapping
                : ModuleCompositionCompletionPoint.ServiceRegistration;
            if (completionPoint != expectedPoint)
            {
                throw new InvalidOperationException(
                    $"Monica composition for {HostBuilderType?.FullName ?? "the current host"} must complete at {expectedPoint}, not {completionPoint}.");
            }

            if (_completionAttempted)
            {
                if (_completionFailure is not null)
                {
                    throw new InvalidOperationException(
                        $"Monica composition previously failed at {_pendingCompletionPoint}.",
                        _completionFailure);
                }

                return false;
            }

            if (completionPoint == ModuleCompositionCompletionPoint.EndpointMapping && !_mapMonicaInvoked)
            {
                throw new InvalidOperationException("MapMonica() must claim endpoint mapping before Web composition can complete.");
            }

            _completionAttempted = true;
            _pendingCompletionPoint = completionPoint;
            _revision++;
            return true;
        }
    }

    /// <summary>
    /// Records successful completion after final composition diagnostics have passed.
    /// </summary>
    internal void CommitCompletion()
    {
        lock (_gate)
        {
            if (!_completionAttempted || _pendingCompletionPoint is null)
            {
                throw new InvalidOperationException("Monica composition completion has not started.");
            }

            _completionPoint = _pendingCompletionPoint;
            _revision++;
        }
    }

    /// <summary>
    /// Records a terminal completion failure without presenting the composition as merely incomplete.
    /// </summary>
    internal void FailCompletion(Exception exception, ModuleCompositionFailureKind failureKind)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_gate)
        {
            if (_completionFailure is not null)
            {
                return;
            }

            _completionAttempted = true;
            _pendingCompletionPoint ??= _requiresEndpointMapping
                ? ModuleCompositionCompletionPoint.EndpointMapping
                : ModuleCompositionCompletionPoint.ServiceRegistration;
            _completionFailure = exception;
            _failureKind = failureKind;
            _revision++;
        }
    }

    /// <summary>Captures detached terminal state without exposing raw exception details.</summary>
    internal ModuleCompositionDiagnosticsState CaptureDiagnostics()
    {
        lock (_gate)
        {
            return new ModuleCompositionDiagnosticsState(
                _revision,
                HostBuilderType,
                _completionPoint,
                _completionFailure is not null,
                _failureKind);
        }
    }

    /// <summary>
    /// Returns a deterministic startup validation failure when Web composition is incomplete.
    /// </summary>
    internal string? GetStartupValidationFailure()
    {
        lock (_gate)
        {
            if (_completionFailure is not null)
            {
                return $"Monica composition failed at {_pendingCompletionPoint}: {_completionFailure.Message}";
            }

            if (!_requiresEndpointMapping || _completionPoint == ModuleCompositionCompletionPoint.EndpointMapping)
            {
                return null;
            }

            return !_useMonicaInvoked
                ? "Monica Web composition is incomplete. Call UseMonica() and MapMonica() before starting the host."
                : "Monica Web composition is incomplete. Call MapMonica() after UseMonica() and before starting the host.";
        }
    }

    /// <summary>
    /// Clears all host composition transitions for an isolated fixture rebuild.
    /// </summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _applicationBuilder = null;
            _completionAttempted = false;
            _mapMonicaInvoked = false;
            _pendingCompletionPoint = null;
            _requiresEndpointMapping = false;
            _useMonicaInvoked = false;
            _completionFailure = null;
            _failureKind = null;
            _completionPoint = null;
            HostBuilderType = null;
            _revision++;
        }
    }

    private void EnsureWebComposition()
    {
        if (!_requiresEndpointMapping)
        {
            throw new InvalidOperationException(
                "UseMonica() and MapMonica() are available only for Monica compositions created with WebApplicationBuilder.");
        }
    }

    private void EnsureCompositionIsPending()
    {
        if (_completionPoint is not null)
        {
            throw new InvalidOperationException(
                $"Monica composition has already completed at {_completionPoint}.");
        }
    }
}

/// <summary>Contains detached, sanitized composition lifecycle state for diagnostics projection.</summary>
internal sealed record ModuleCompositionDiagnosticsState(
    long Revision,
    Type? HostBuilderType,
    ModuleCompositionCompletionPoint? CompletionPoint,
    bool IsFailed,
    ModuleCompositionFailureKind? FailureKind)
{
    /// <summary>Gets whether composition reached a successful or failed terminal boundary.</summary>
    internal bool IsTerminal => CompletionPoint is not null || IsFailed;

    /// <summary>Gets whether composition completed successfully.</summary>
    internal bool IsSucceeded => CompletionPoint is not null && !IsFailed;
}
