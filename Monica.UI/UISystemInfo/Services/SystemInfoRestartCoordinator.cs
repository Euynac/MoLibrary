using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.UI.UISystemInfo.Services;

internal enum SystemInfoRestartStatus
{
    Scheduled,
    Disabled,
    AlreadyPending
}

internal readonly record struct SystemInfoRestartRequest(
    SystemInfoRestartStatus Status,
    TimeSpan Delay);

internal sealed class SystemInfoRestartCoordinator(
    IHostApplicationLifetime applicationLifetime,
    IOptions<ModuleSystemInfoUIOption> options,
    ILogger<SystemInfoRestartCoordinator> logger)
{
    private int _restartRequested;
    // Retain the scheduled operation on the host-owned coordinator instead of creating an unowned fire-and-forget task.
    private Task? _scheduledShutdown;

    internal Task? ScheduledShutdown => _scheduledShutdown;

    internal SystemInfoRestartRequest Request()
    {
        var currentOptions = options.Value;
        if (!currentOptions.EnableSelfRestartAction)
        {
            logger.LogWarning("Self restart was requested but the action is disabled.");
            return new SystemInfoRestartRequest(SystemInfoRestartStatus.Disabled, TimeSpan.Zero);
        }

        if (Interlocked.CompareExchange(ref _restartRequested, 1, 0) != 0)
        {
            logger.LogWarning("Ignoring duplicate self restart request because shutdown is already scheduled.");
            return new SystemInfoRestartRequest(SystemInfoRestartStatus.AlreadyPending, TimeSpan.Zero);
        }

        var shutdownDelay = currentOptions.SelfRestartDelay < TimeSpan.Zero
            ? TimeSpan.Zero
            : currentOptions.SelfRestartDelay;

        logger.LogWarning(
            "Graceful self restart requested. Shutdown will begin in {Delay}.",
            shutdownDelay);

        _scheduledShutdown = ScheduleShutdownAsync(shutdownDelay);
        return new SystemInfoRestartRequest(SystemInfoRestartStatus.Scheduled, shutdownDelay);
    }

    private async Task ScheduleShutdownAsync(TimeSpan shutdownDelay)
    {
        try
        {
            // Let the initiating HTTP or circuit operation publish its result before shutdown can begin.
            await Task.Yield();

            if (shutdownDelay > TimeSpan.Zero)
            {
                await Task.Delay(
                        shutdownDelay,
                        applicationLifetime.ApplicationStopping)
                    .ConfigureAwait(false);
            }

            if (applicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                return;
            }

            applicationLifetime.StopApplication();
        }
        catch (OperationCanceledException) when (applicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            // The host is already stopping; the delayed restart request must not outlive it.
        }
        catch (Exception ex)
        {
            _scheduledShutdown = null;
            Interlocked.Exchange(ref _restartRequested, 0);
            logger.LogError(ex, "Failed while scheduling application shutdown for a self restart request.");
        }
    }
}
