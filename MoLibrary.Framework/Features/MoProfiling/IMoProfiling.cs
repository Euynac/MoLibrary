namespace MoLibrary.Framework.Features.MoProfiling;

/// <summary>
/// 程序性能监测接口
/// </summary>
public interface IMoProfiling
{
    /// <summary>
    /// 获取当前进程的CPU使用率（百分比）
    /// </summary>
    /// <returns>CPU使用率，范围0-100</returns>
    Task<string> GetCpuUsageAsync();

    /// <summary>
    /// 获取当前进程的内存使用情况(单位: MB)
    /// </summary>
    /// <returns>内存使用信息</returns>
    Task<double> GetMemoryUsageAsync();
}