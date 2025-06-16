using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoLibrary.Framework.Features.MoProfiling
{
    /// <summary>
    /// 程序性能监测接口，提供CPU、内存及其他关键指标的监控功能
    /// </summary>
    public interface IMoProfiling
    {
        /// <summary>
        /// 获取当前进程的CPU使用率（百分比）
        /// </summary>
        /// <returns>CPU使用率，范围0-100</returns>
        Task<double> GetCpuUsageAsync();

        /// <summary>
        /// 获取当前进程的内存使用情况
        /// </summary>
        /// <returns>内存使用信息</returns>
        Task<MemoryUsageInfo> GetMemoryUsageAsync();

        /// <summary>
        /// 获取当前进程的工作集内存大小（MB）
        /// </summary>
        /// <returns>工作集内存大小</returns>
        Task<long> GetWorkingSetMemoryAsync();

        /// <summary>
        /// 获取当前进程的私有内存大小（MB）
        /// </summary>
        /// <returns>私有内存大小</returns>
        Task<long> GetPrivateMemoryAsync();

        /// <summary>
        /// 获取当前进程的线程数量
        /// </summary>
        /// <returns>线程数量</returns>
        Task<int> GetThreadCountAsync();

        /// <summary>
        /// 获取当前进程的句柄数量
        /// </summary>
        /// <returns>句柄数量</returns>
        Task<int> GetHandleCountAsync();

        /// <summary>
        /// 获取当前进程的运行时间
        /// </summary>
        /// <returns>进程运行时间</returns>
        Task<TimeSpan> GetProcessUptimeAsync();

        /// <summary>
        /// 获取系统总内存信息
        /// </summary>
        /// <returns>系统内存信息</returns>
        Task<SystemMemoryInfo> GetSystemMemoryInfoAsync();

        /// <summary>
        /// 获取综合的性能快照
        /// </summary>
        /// <returns>性能快照信息</returns>
        Task<PerformanceSnapshot> GetPerformanceSnapshotAsync();

        /// <summary>
        /// 开始连续监控性能指标
        /// </summary>
        /// <param name="interval">监控间隔</param>
        /// <param name="callback">监控数据回调</param>
        /// <returns>监控任务</returns>
        Task StartMonitoringAsync(TimeSpan interval, Action<PerformanceSnapshot> callback);

        /// <summary>
        /// 停止连续监控
        /// </summary>
        void StopMonitoring();
    }

    /// <summary>
    /// 内存使用信息
    /// </summary>
    public class MemoryUsageInfo
    {
        /// <summary>
        /// 工作集内存（MB）
        /// </summary>
        public long WorkingSetMemory { get; set; }

        /// <summary>
        /// 私有内存（MB）
        /// </summary>
        public long PrivateMemory { get; set; }

        /// <summary>
        /// 虚拟内存（MB）
        /// </summary>
        public long VirtualMemory { get; set; }

        /// <summary>
        /// GC堆内存（MB）
        /// </summary>
        public long GcMemory { get; set; }

        /// <summary>
        /// 内存使用率（相对于系统总内存的百分比）
        /// </summary>
        public double MemoryUsagePercentage { get; set; }
    }

    /// <summary>
    /// 系统内存信息
    /// </summary>
    public class SystemMemoryInfo
    {
        /// <summary>
        /// 系统总内存（MB）
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// 系统可用内存（MB）
        /// </summary>
        public long AvailableMemory { get; set; }

        /// <summary>
        /// 系统已使用内存（MB）
        /// </summary>
        public long UsedMemory { get; set; }

        /// <summary>
        /// 系统内存使用率（百分比）
        /// </summary>
        public double MemoryUsagePercentage { get; set; }
    }

    /// <summary>
    /// 性能快照信息
    /// </summary>
    public class PerformanceSnapshot
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// CPU使用率（百分比）
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// 内存使用信息
        /// </summary>
        public MemoryUsageInfo MemoryUsage { get; set; }

        /// <summary>
        /// 线程数量
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// 句柄数量
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// 进程运行时间
        /// </summary>
        public TimeSpan ProcessUptime { get; set; }

        /// <summary>
        /// 系统内存信息
        /// </summary>
        public SystemMemoryInfo SystemMemory { get; set; }
    }
}