using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.UISystemInfo.Models;
using Monica.Tool.Results;

namespace Monica.Framework.UI.UISystemInfo.Services;

/// <summary>
/// Provides system information for the System Info UI page.
/// </summary>
/// <param name="logger">The logger.</param>
/// <param name="localizer">The localizer.</param>
public class SystemInfoService(
    ILogger<SystemInfoService> logger,
    IStringLocalizer<SystemInfoResource> localizer)
{
    /// <summary>
    /// Gets system information.
    /// </summary>
    /// <param name="simple">Whether to return a simplified response.</param>
    /// <returns>The system information result.</returns>
    public async Task<Res<SystemInfoResponse>> GetSystemInfoAsync(bool? simple = null)
    {
        try
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
            {
                logger.LogWarning("Unable to resolve entry assembly information.");
                return Res.Fail(localizer["Service:Errors:EntryAssemblyUnavailable"].Value);
            }

            var fileInfo = FileVersionInfo.GetVersionInfo(entryAssembly.Location);
            var buildTime = File.GetLastWriteTime(fileInfo.FileName);
            var processStartTime = Process.GetCurrentProcess().StartTime;

            var response = new SystemInfoResponse
            {
                BuildTime = buildTime,
                LocalTime = DateTime.Now,
                UtcTime = DateTime.UtcNow,
                ProcessStartTime = processStartTime
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
} 
