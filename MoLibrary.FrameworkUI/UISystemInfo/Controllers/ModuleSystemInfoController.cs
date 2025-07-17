using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Tool.MoResponse;

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
            var environmentInfo = new
            {
                Environment.Version,
                Environment.UserName,
                Environment.MachineName,
                Environment.OSVersion,
                Environment.ProcessId,
                Environment.CurrentDirectory,
                Environment.HasShutdownStarted,
                Environment.Is64BitOperatingSystem,
                Environment.Is64BitProcess,
                Environment.IsPrivilegedProcess,
                Environment.TickCount,
                Environment.UserDomainName,
                Environment.WorkingSet,
                Environment.SystemPageSize,
                environments = Environment.GetEnvironmentVariables(),
                Environment.UserInteractive,
                Environment.ProcessPath,
            };

            if (simple is not true)
            {
                var infos = new
                {
                    buildTime,
                    localTime = DateTime.Now,
                    utcTime = DateTime.UtcNow,
                    fileInfo,
                    environmentInfo,
                };

                return Res.Ok(infos).GetResponse(this);
            }
            else
            {
                var infos = new
                {
                    buildTime,
                    localTime = DateTime.Now,
                    utcTime = DateTime.UtcNow,
                    fileInfo.ProductVersion
                };

                return Res.Ok(infos).GetResponse(this);
            }
        }
        catch (Exception ex)
        {
            return Res.Fail($"获取系统信息失败: {ex.Message}").GetResponse(this);
        }
    }
} 