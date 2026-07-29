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

/// <summary>
/// Owns the composition transitions for one Monica host.
/// </summary>
internal sealed class ModuleCompositionState
{
    private readonly object _gate = new();
    private IApplicationBuilder? _applicationBuilder;
    private ModuleCompositionCompletionPoint? _completionPoint;
    private Exception? _completionFailure;
    private bool _completionAttempted;
    private bool _mapMonicaInvoked;
    private ModuleCompositionCompletionPoint? _pendingCompletionPoint;
    private bool _requiresEndpointMapping;
    private bool _useMonicaInvoked;

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
        }
    }

    /// <summary>
    /// Records a terminal completion failure without presenting the composition as merely incomplete.
    /// </summary>
    internal void FailCompletion(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_gate)
        {
            if (!_completionAttempted || _pendingCompletionPoint is null)
            {
                throw new InvalidOperationException("Monica composition completion has not started.");
            }

            _completionFailure = exception;
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
            _completionPoint = null;
            HostBuilderType = null;
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
