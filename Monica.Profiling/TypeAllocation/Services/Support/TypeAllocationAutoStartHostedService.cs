using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Modules;
using Monica.Profiling.TypeAllocation.Models;

namespace Monica.Profiling.TypeAllocation.Services.Support;

/// <summary>
/// Starts type allocation collection automatically when the module option enables it.
/// </summary>
internal sealed class TypeAllocationAutoStartHostedService(
    TypeAllocationTrackingService typeAllocationTrackingService,
    IOptions<ModuleTypeAllocationOption> options,
    ILogger<TypeAllocationAutoStartHostedService> logger,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions) : MoHostedService(observableManager, hostedServiceOptions, logger)
{
    /// <inheritdoc />
    public override string ServiceName => nameof(TypeAllocationAutoStartHostedService);

    /// <inheritdoc />
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.TypeAllocation);

    /// <inheritdoc />
    protected override Task OnStartingAsync(CancellationToken cancellationToken)
    {
        var opt = options.Value;
        if (!opt.AutoStartCollection)
        {
            return Task.CompletedTask;
        }

        var mode = opt.DefaultSamplingMode;
        if (mode == AllocationSamplingMode.Disabled)
        {
            RecordState("Type allocation auto start skipped because the default sampling mode is disabled.", HostedServiceState.Degraded, logLevel: LogLevel.Warning);
            logger.LogWarning("AutoStartCollection is enabled but DefaultSamplingMode is Disabled. Skipping auto start.");
            return Task.CompletedTask;
        }

        try
        {
            RecordState($"Starting type allocation collection automatically with mode {mode}.", HostedServiceState.Executing);
            logger.LogInformation("Starting type allocation collection automatically with mode {Mode}.", mode);
            typeAllocationTrackingService.StartCollection(mode);
            RecordState("Type allocation collection started.", HostedServiceState.Running);
        }
        catch (Exception ex)
        {
            RecordState("Failed to auto start type allocation collection.", HostedServiceState.Degraded, ex);
            logger.LogError(ex, "Failed to auto start type allocation collection.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task OnStoppingAsync(CancellationToken cancellationToken)
    {
        if (typeAllocationTrackingService.IsCollecting)
        {
            RecordState("Stopping type allocation collection.", HostedServiceState.Stopping);
            logger.LogInformation("Stopping type allocation collection.");
            typeAllocationTrackingService.StopCollection();
            RecordState("Type allocation collection stopped.", HostedServiceState.Stopped);
        }

        return Task.CompletedTask;
    }
}
