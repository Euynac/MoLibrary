using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MoLibrary.FrameworkUI.UISystemInfo.Models;

/// <summary>
/// 系统信息响应模型
/// </summary>
public class SystemInfoResponse
{
    /// <summary>
    /// 构建时间
    /// </summary>
    public DateTime BuildTime { get; set; }

    /// <summary>
    /// 本地时间
    /// </summary>
    public DateTime LocalTime { get; set; }

    /// <summary>
    /// UTC时间
    /// </summary>
    public DateTime UtcTime { get; set; }

    /// <summary>
    /// 产品版本（简化模式）
    /// </summary>
    public string? ProductVersion { get; set; }

    /// <summary>
    /// 文件信息（详细模式）
    /// </summary>
    public FileVersionInfo? FileInfo { get; set; }

    /// <summary>
    /// 环境信息（详细模式）
    /// </summary>
    public EnvironmentInfo? EnvironmentInfo { get; set; }
}

/// <summary>
/// 环境信息
/// </summary>
public class EnvironmentInfo
{
    /// <summary>
    /// .NET版本
    /// </summary>
    public Version? Version { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 机器名
    /// </summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// 操作系统版本
    /// </summary>
    public OperatingSystem? OSVersion { get; set; }

    /// <summary>
    /// 进程ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 当前目录
    /// </summary>
    public string? CurrentDirectory { get; set; }

    /// <summary>
    /// 是否已开始关闭
    /// </summary>
    public bool HasShutdownStarted { get; set; }

    /// <summary>
    /// 是否64位操作系统
    /// </summary>
    public bool Is64BitOperatingSystem { get; set; }

    /// <summary>
    /// 是否64位进程
    /// </summary>
    public bool Is64BitProcess { get; set; }

    /// <summary>
    /// 是否特权进程
    /// </summary>
    public bool IsPrivilegedProcess { get; set; }

    /// <summary>
    /// 系统滴答计数
    /// </summary>
    public int TickCount { get; set; }

    /// <summary>
    /// 用户域名
    /// </summary>
    public string? UserDomainName { get; set; }

    /// <summary>
    /// 工作集大小
    /// </summary>
    public long WorkingSet { get; set; }

    /// <summary>
    /// 系统页面大小
    /// </summary>
    public int SystemPageSize { get; set; }

    /// <summary>
    /// 环境变量
    /// </summary>
    public IDictionary<string, object?>? Environments { get; set; }

    /// <summary>
    /// 是否用户交互式
    /// </summary>
    public bool UserInteractive { get; set; }

    /// <summary>
    /// 进程路径
    /// </summary>
    public string? ProcessPath { get; set; }
} 