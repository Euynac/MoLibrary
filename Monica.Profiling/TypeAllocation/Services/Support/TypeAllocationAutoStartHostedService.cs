using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Profiling.TypeAllocation.Models;

namespace Monica.Profiling.TypeAllocation.Services.Support;

/// <summary>
/// Starts type allocation collection automatically when the module option enables it.
/// </summary>
internal sealed class TypeAllocationAutoStartHostedService(
    TypeAllocationTrackingService typeAllocationTrackingService,
    IOptions<ModuleTypeAllocationOption> options,
    ILogger<TypeAllocationAutoStartHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opt = options.Value;
        if (!opt.AutoStartCollection)
        {
            return Task.CompletedTask;
        }

        var mode = opt.DefaultSamplingMode;
        if (mode == AllocationSamplingMode.Disabled)
        {
            logger.LogWarning("AutoStartCollection is enabled but DefaultSamplingMode is Disabled. Skipping auto start.");
            return Task.CompletedTask;
        }

        try
        {
            logger.LogInformation("Starting type allocation collection automatically with mode {Mode}.", mode);
            typeAllocationTrackingService.StartCollection(mode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto start type allocation collection.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (typeAllocationTrackingService.IsCollecting)
        {
            logger.LogInformation("Stopping type allocation collection.");
            typeAllocationTrackingService.StopCollection();
        }

        return Task.CompletedTask;
    }
}
