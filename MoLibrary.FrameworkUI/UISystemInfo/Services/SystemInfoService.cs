using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MoLibrary.FrameworkUI.UISystemInfo.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UISystemInfo.Services;

/// <summary>
/// 系统信息服务，直接实现业务逻辑
/// </summary>
/// <param name="logger">日志服务</param>
public class SystemInfoService(ILogger<SystemInfoService> logger)
{
    /// <summary>
    /// 获取系统信息
    /// </summary>
    /// <param name="simple">是否简化输出</param>
    /// <returns>系统信息</returns>
    public async Task<Res<SystemInfoResponse>> GetSystemInfoAsync(bool? simple = null)
    {
        try
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
            {
                logger.LogWarning("无法获取入口程序集信息");
                return Res.Fail("无法获取入口程序集信息");
            }

            var fileInfo = FileVersionInfo.GetVersionInfo(entryAssembly.Location);
            var buildTime = System.IO.File.GetLastWriteTime(fileInfo.FileName);

            var response = new SystemInfoResponse
            {
                BuildTime = buildTime,
                LocalTime = DateTime.Now,
                UtcTime = DateTime.UtcNow
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
            }

            logger.LogDebug("成功获取系统信息，简化模式: {Simple}", simple ?? false);
            return Res.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取系统信息失败");
            return Res.Fail($"获取系统信息失败: {ex.Message}");
        }
    }
} 