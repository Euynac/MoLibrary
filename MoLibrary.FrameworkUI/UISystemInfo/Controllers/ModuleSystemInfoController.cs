using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Tool.MoResponse;
using MoLibrary.FrameworkUI.UISystemInfo.Models;

namespace MoLibrary.FrameworkUI.UISystemInfo.Controllers;

/// <summary>
/// 系统信息相关功能Controller
/// </summary>
[ApiController]
public class ModuleSystemInfoController : MoModuleControllerBase
{
    /// <summary>
    /// 获取微服务信息
    /// </summary>
    /// <param name="simple">是否简化输出</param>
    /// <returns>系统信息</returns>
    [HttpGet("system/info")]
    public IActionResult GetSystemInfo([FromQuery] bool? simple = null)
    {
        try
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
            {
                return Res.Fail("无法获取入口程序集信息").GetResponse(this);
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

            return Res.Ok(response).GetResponse(this);
        }
        catch (Exception ex)
        {
            return Res.Fail($"获取系统信息失败: {ex.Message}").GetResponse(this);
        }
    }
} 