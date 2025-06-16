using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MoLibrary.Framework.Features.MoProfiling
{
    /// <summary>
    /// MoProfiling服务扩展方法
    /// </summary>
    public static class MoProfilingExtensions
    {
        /// <summary>
        /// 获取格式化的内存使用信息字符串
        /// </summary>
        /// <param name="memoryInfo">内存使用信息</param>
        /// <returns>格式化的内存信息</returns>
        public static string ToFormattedString(this MemoryUsageInfo memoryInfo)
        {
            if (memoryInfo == null) return "N/A";

            return $"WorkingSet: {memoryInfo.WorkingSetMemory:N0} MB, " +
                   $"Private: {memoryInfo.PrivateMemory:N0} MB, " +
                   $"Virtual: {memoryInfo.VirtualMemory:N0} MB, " +
                   $"GC: {memoryInfo.GcMemory:N0} MB, " +
                   $"Usage: {memoryInfo.MemoryUsagePercentage:F1}%";
        }

        /// <summary>
        /// 获取格式化的系统内存信息字符串
        /// </summary>
        /// <param name="systemMemory">系统内存信息</param>
        /// <returns>格式化的系统内存信息</returns>
        public static string ToFormattedString(this SystemMemoryInfo systemMemory)
        {
            if (systemMemory == null) return "N/A";

            return $"Total: {systemMemory.TotalMemory:N0} MB, " +
                   $"Available: {systemMemory.AvailableMemory:N0} MB, " +
                   $"Used: {systemMemory.UsedMemory:N0} MB, " +
                   $"Usage: {systemMemory.MemoryUsagePercentage:F1}%";
        }

        /// <summary>
        /// 获取格式化的性能快照信息字符串
        /// </summary>
        /// <param name="snapshot">性能快照</param>
        /// <returns>格式化的性能快照信息</returns>
        public static string ToFormattedString(this PerformanceSnapshot snapshot)
        {
            if (snapshot == null) return "N/A";

            return $"[{snapshot.Timestamp:yyyy-MM-dd HH:mm:ss}] " +
                   $"CPU: {snapshot.CpuUsage:F1}%, " +
                   $"Memory: {snapshot.MemoryUsage?.WorkingSetMemory:N0} MB, " +
                   $"Threads: {snapshot.ThreadCount}, " +
                   $"Handles: {snapshot.HandleCount}, " +
                   $"Uptime: {FormatTimeSpan(snapshot.ProcessUptime)}";
        }
        /// <summary>
        /// 快速获取当前进程的基本性能信息
        /// </summary>
        /// <param name="profiling">性能监测服务</param>
        /// <returns>基本性能信息字符串</returns>
        public static async Task<string> GetQuickStatusAsync(this IMoProfiling profiling)
        {
            try
            {
                var cpuTask = profiling.GetCpuUsageAsync();
                var memoryTask = profiling.GetWorkingSetMemoryAsync();
                var threadTask = profiling.GetThreadCountAsync();

                await Task.WhenAll(cpuTask, memoryTask, threadTask);

                var cpuUsage = await cpuTask;
                var memoryUsage = await memoryTask;
                var threadCount = await threadTask;

                return $"CPU: {cpuUsage:F1}%, Memory: {memoryUsage:N0} MB, Threads: {threadCount}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 执行性能监测并返回执行时间和性能数据
        /// </summary>
        /// <param name="profiling">性能监测服务</param>
        /// <param name="action">要执行的操作</param>
        /// <returns>执行时间和性能数据</returns>
        public static async Task<(TimeSpan ExecutionTime, PerformanceSnapshot BeforeSnapshot, PerformanceSnapshot AfterSnapshot)> 
            ProfileActionAsync(this IMoProfiling profiling, Func<Task> action)
        {
            var beforeSnapshot = await profiling.GetPerformanceSnapshotAsync();
            var startTime = DateTime.UtcNow;
            
            await action();
            
            var endTime = DateTime.UtcNow;
            var afterSnapshot = await profiling.GetPerformanceSnapshotAsync();
            
            return (endTime - startTime, beforeSnapshot, afterSnapshot);
        }

        /// <summary>
        /// 执行性能监测并返回执行时间和性能数据（同步版本）
        /// </summary>
        /// <param name="profiling">性能监测服务</param>
        /// <param name="action">要执行的操作</param>
        /// <returns>执行时间和性能数据</returns>
        public static async Task<(TimeSpan ExecutionTime, PerformanceSnapshot BeforeSnapshot, PerformanceSnapshot AfterSnapshot)> 
            ProfileAction(this IMoProfiling profiling, Action action)
        {
            return await ProfileActionAsync(profiling, () =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// 格式化时间跨度
        /// </summary>
        /// <param name="timeSpan">时间跨度</param>
        /// <returns>格式化的时间字符串</returns>
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
                return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m";
            if (timeSpan.TotalHours >= 1)
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            return $"{timeSpan.TotalSeconds:F1}s";
        }
    }
} 