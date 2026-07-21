using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Modules;
using Monica.UI.Localization;
using Monica.UI.UISystemInfo.Models;

namespace Monica.UI.UISystemInfo.Support;

/// <summary>
/// Provides system information for the System Info UI page.
/// </summary>
/// <param name="logger">The logger.</param>
/// <param name="localizer">The localizer.</param>
/// <param name="serverAddressesFeature">The server feature that exposes runtime listening addresses.</param>
/// <param name="applicationLifetime">The host lifetime controller used to request graceful shutdown.</param>
/// <param name="options">The system information UI options.</param>
public class SystemInfoService(
    ILogger<SystemInfoService> logger,
    IStringLocalizer<SystemInfoResource> localizer,
    IServerAddressesFeature serverAddressesFeature,
    IHostApplicationLifetime applicationLifetime,
    IOptions<ModuleSystemInfoUIOption> options)
{
    private readonly ModuleSystemInfoUIOption _options = options.Value;
    private int _restartRequested;

    /// <summary>
    /// Gets system information.
    /// </summary>
    /// <param name="simple">Whether to return a simplified response.</param>
    /// <returns>The system information result.</returns>
    public async Task<Res<SystemInfoResponse>> GetSystemInfoAsync(bool? simple = null)
    {
        try
        {
            if (OperatingSystem.IsBrowser())
            {
                logger.LogWarning("System information is unavailable when running in a browser runtime.");
                return Res.Fail(localizer["Service:Errors:BrowserRuntimeUnsupported"].Value);
            }

            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
            {
                logger.LogWarning("Unable to resolve entry assembly information.");
                return Res.Fail(localizer["Service:Errors:EntryAssemblyUnavailable"].Value);
            }

            var fileInfo = FileVersionInfo.GetVersionInfo(entryAssembly.Location);
            var buildTime = File.GetLastWriteTime(fileInfo.FileName);
            var processStartTime = Process.GetCurrentProcess().StartTime;
            var utcNow = DateTimeOffset.UtcNow;

            var response = new SystemInfoResponse
            {
                BuildTime = buildTime,
                LocalTime = utcNow.LocalDateTime,
                UtcTime = utcNow.UtcDateTime,
                TimeZone = SystemTimeZone.CaptureLocal(utcNow),
                ProcessStartTime = processStartTime,
                ListeningAddresses = GetListeningAddresses()
            };

            if (simple is not true)
            {
                response.FileInfo = fileInfo;
                response.EnvironmentInfo = new EnvironmentInfo
                {
                    Version = Environment.Version,
                    UserName = Environment.UserName,
                    MachineName = Environment.MachineName,
                    OSVersion = Environment.OSVersion,
                    ProcessId = Environment.ProcessId,
                    ProcessStartTime = processStartTime,
                    CurrentDirectory = Environment.CurrentDirectory,
                    HasShutdownStarted = Environment.HasShutdownStarted,
                    Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                    Is64BitProcess = Environment.Is64BitProcess,
                    IsPrivilegedProcess = Environment.IsPrivilegedProcess,
                    TickCount = Environment.TickCount,
                    UserDomainName = Environment.UserDomainName,
                    WorkingSet = Environment.WorkingSet,
                    SystemPageSize = Environment.SystemPageSize,
                    Environments = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                        .ToDictionary(entry => entry.Key.ToString()!, entry => entry.Value),
                    UserInteractive = Environment.UserInteractive,
                    ProcessPath = Environment.ProcessPath
                };
            }
            else
            {
                response.ProductVersion = fileInfo.ProductVersion;
                // .NET 8 or later will automatically include the git commit source revision in the informational version
                // 
                // The described behavior can be disabled by adding
                // 
                // <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
                // to the project file.
                // 
                // It seems to be introduced by SourceLink related changes in the SDK 8 version. I found an existing github issue as well: https://github.com/dotnet/sdk/issues/34568
            }

            logger.LogDebug("Successfully retrieved system information. Simple mode: {Simple}", simple ?? false);
            return Res.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve system information.");
            return Res.Fail(localizer["Service:Errors:GetSystemInfoFailed", ex.Message].Value);
        }
    }

    private IReadOnlyList<string> GetListeningAddresses()
    {
        if (serverAddressesFeature.Addresses.Count == 0)
        {
            return [];
        }

        return serverAddressesFeature.Addresses
            .Where(static address => !string.IsNullOrWhiteSpace(address))
            .Select(static address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Requests a graceful shutdown for the current process so an external supervisor can restart it.
    /// </summary>
    /// <returns>
    /// A successful response when shutdown has been scheduled.
    /// This is not a true in-process restart. The service only starts again when the hosting environment restarts it.
    /// </returns>
    public Task<Res> RequestSelfRestartAsync()
    {
        if (!_options.EnableSelfRestartAction)
        {
            logger.LogWarning("Self restart was requested but the action is disabled.");
            return Task.FromResult(Res.Fail(localizer["Service:Errors:SelfRestartDisabled"].Value));
        }

        if (Interlocked.CompareExchange(ref _restartRequested, 1, 0) != 0)
        {
            logger.LogWarning("Ignoring duplicate self restart request because shutdown is already scheduled.");
            return Task.FromResult(Res.Fail(localizer["Service:Errors:RestartAlreadyRequested"].Value));
        }

        var shutdownDelay = _options.SelfRestartDelay < TimeSpan.Zero ? TimeSpan.Zero : _options.SelfRestartDelay;
        logger.LogWarning(
            "Graceful self restart requested. Shutdown will begin in {Delay}.",
            shutdownDelay);

        _ = Task.Run(async () =>
        {
            try
            {
                if (shutdownDelay > TimeSpan.Zero)
                {
                    await Task.Delay(shutdownDelay);
                }

                applicationLifetime.StopApplication();
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _restartRequested, 0);
                logger.LogError(ex, "Failed while scheduling application shutdown for a self restart request.");
            }
        });

        return Task.FromResult(Res.Ok(localizer["Service:Messages:RestartRequested", shutdownDelay.TotalSeconds].Value));
    }
}
