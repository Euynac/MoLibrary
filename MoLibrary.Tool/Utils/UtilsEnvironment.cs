using System;

namespace MoLibrary.Tool.Utils;

public static class UtilsEnvironment
{
    /// <summary>
    /// 依赖注入中可使用IHostEnvironment.IsDevelopment()判断是否为开发环境
    /// </summary>
    /// <returns></returns>
    public static bool IsDevelopment() => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) is true;

    /// <summary>
    /// 是否处于Migration环境下
    /// </summary>
    public static bool IsRunningMigration { get; set; } = false;

}