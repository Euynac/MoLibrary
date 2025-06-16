using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace MoLibrary.Framework.Features.MoProfiling
{
    /// <summary>
    /// 程序性能监测实现类
    /// </summary>
    public class MoProfiling : IMoProfiling, IDisposable
    {
        private readonly ILogger<MoProfiling> _logger;
        private readonly Process _currentProcess;
        private CancellationTokenSource _monitoringCancellation;
        private Task _monitoringTask;
        private readonly object _lockObject = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public MoProfiling(ILogger<MoProfiling> logger = null)
        {
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
        }

        /// <summary>
        /// 获取当前进程的CPU使用率（百分比）
        /// </summary>
        /// <returns>CPU使用率，范围0-100</returns>
        public async Task<double> GetCpuUsageAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // 手动计算CPU使用率
                    var startTime = DateTime.UtcNow;
                    var startCpuUsage = _currentProcess.TotalProcessorTime;

                    await Task.Delay(500); // 替代 Thread.Sleep，避免阻塞线程

                    var endTime = DateTime.UtcNow;
                    var endCpuUsage = _currentProcess.TotalProcessorTime;
                    
                    var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                    var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                    var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                    
                    return Math.Round(cpuUsageTotal * 100, 2);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to get CPU usage");
                    return 0.0;
                }
            });
        }

        /// <summary>
        /// 获取当前进程的内存使用情况
        /// </summary>
        /// <returns>内存使用信息</returns>
        public async Task<MemoryUsageInfo> GetMemoryUsageAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    _currentProcess.Refresh();
                    var systemMemory = await GetSystemMemoryInfoAsync();
                    var gcMemory = GC.GetTotalMemory(false);

                    return new MemoryUsageInfo
                    {
                        WorkingSetMemory = _currentProcess.WorkingSet64 / (1024 * 1024),
                        PrivateMemory = _currentProcess.PrivateMemorySize64 / (1024 * 1024),
                        VirtualMemory = _currentProcess.VirtualMemorySize64 / (1024 * 1024),
                        GcMemory = gcMemory / (1024 * 1024),
                        MemoryUsagePercentage = Math.Round((_currentProcess.WorkingSet64 / (double)systemMemory.TotalMemory) * 100, 2)
                    };
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to get memory usage");
                    return new MemoryUsageInfo();
                }
            });
        }

        /// <summary>
        /// 获取当前进程的工作集内存大小（MB）
        /// </summary>
        /// <returns>工作集内存大小</returns>
        public async Task<long> GetWorkingSetMemoryAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    _currentProcess.Refresh();
                    return _currentProcess.WorkingSet64 / (1024 * 1024);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to get working set memory");
                    return 0;
                }
            });
        }

        /// <summary>
        /// 获取当前进程的私有内存大小（MB）
        /// </summary>
        /// <returns>私有内存大小</returns>
        public async Task<long> GetPrivateMemoryAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    _currentProcess.Refresh();
                    return _currentProcess.PrivateMemorySize64 / (1024 * 1024);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to get private memory");
                    return 0;
                }
            });
        }

        /// <summary>
        /// 获取当前进程的线程数量
        /// </summary>
        /// <returns>线程数量</returns>
        public async Task<int> GetThreadCountAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    _currentProcess.Refresh();
                    return _currentProcess.Threads.Count;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to get thread count");
                    return 0;
                }
            });
        }

        /// <summary>
        /// 获取当前进程的句柄数量
        /// </summary>
        /// <returns>句柄数量</returns>
        public async Task<int> GetHandleCountAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    _currentProcess.Refresh();
                    return _currentProcess.HandleCount;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to get handle count");
                    return 0;
                }
            });
        }

        /// <summary>
        /// 获取当前进程的运行时间
        /// </summary>
        /// <returns>进程运行时间</returns>
        public async Task<TimeSpan> GetProcessUptimeAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    _currentProcess.Refresh();
                    return DateTime.Now - _currentProcess.StartTime;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to get process uptime");
                    return TimeSpan.Zero;
                }
            });
        }

        /// <summary>
        /// 获取系统总内存信息
        /// </summary>
        /// <returns>系统内存信息</returns>
        public async Task<SystemMemoryInfo> GetSystemMemoryInfoAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var memoryInfo = new SystemMemoryInfo();
                    
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        var memStatus = new MEMORYSTATUSEX();
                        memStatus.dwLength = (uint)Marshal.SizeOf(memStatus);
                        
                        if (GlobalMemoryStatusEx(ref memStatus))
                        {
                            memoryInfo.TotalMemory = (long)(memStatus.ullTotalPhys / (1024 * 1024));
                            memoryInfo.AvailableMemory = (long)(memStatus.ullAvailPhys / (1024 * 1024));
                            memoryInfo.UsedMemory = memoryInfo.TotalMemory - memoryInfo.AvailableMemory;
                            memoryInfo.MemoryUsagePercentage = Math.Round((double)memoryInfo.UsedMemory / memoryInfo.TotalMemory * 100, 2);
                        }
                    }
                    else
                    {
                        // 对于非Windows平台，使用基本信息
                        memoryInfo.TotalMemory = Environment.WorkingSet / (1024 * 1024);
                    }
                    
                    return memoryInfo;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to get system memory info");
                    return new SystemMemoryInfo();
                }
            });
        }

        /// <summary>
        /// 获取综合的性能快照
        /// </summary>
        /// <returns>性能快照信息</returns>
        public async Task<PerformanceSnapshot> GetPerformanceSnapshotAsync()
        {
            var cpuTask = GetCpuUsageAsync();
            var memoryTask = GetMemoryUsageAsync();
            var threadTask = GetThreadCountAsync();
            var handleTask = GetHandleCountAsync();
            var uptimeTask = GetProcessUptimeAsync();
            var systemMemoryTask = GetSystemMemoryInfoAsync();

            await Task.WhenAll(cpuTask, memoryTask, threadTask, handleTask, uptimeTask, systemMemoryTask);

            return new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CpuUsage = await cpuTask,
                MemoryUsage = await memoryTask,
                ThreadCount = await threadTask,
                HandleCount = await handleTask,
                ProcessUptime = await uptimeTask,
                SystemMemory = await systemMemoryTask
            };
        }

        /// <summary>
        /// 开始连续监控性能指标
        /// </summary>
        /// <param name="interval">监控间隔</param>
        /// <param name="callback">监控数据回调</param>
        /// <returns>监控任务</returns>
        public async Task StartMonitoringAsync(TimeSpan interval, Action<PerformanceSnapshot> callback)
        {
            lock (_lockObject)
            {
                if (_monitoringTask is {IsCompleted: false})
                {
                    throw new InvalidOperationException("Monitoring is already running");
                }

                _monitoringCancellation?.Dispose();
                _monitoringCancellation = new CancellationTokenSource();
            }

            lock (_lockObject)
            {
                _monitoringTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!_monitoringCancellation.Token.IsCancellationRequested)
                        {
                            try
                            {
                                var snapshot = await GetPerformanceSnapshotAsync();
                                callback?.Invoke(snapshot);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error occurred during performance monitoring");
                            }

                            await Task.Delay(interval, _monitoringCancellation.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，不需要记录日志
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Monitoring task failed");
                    }
                }, _monitoringCancellation.Token);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 停止连续监控
        /// </summary>
        public void StopMonitoring()
        {
            lock (_lockObject)
            {
                _monitoringCancellation?.Cancel();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
            _monitoringCancellation?.Dispose();
            _currentProcess?.Dispose();
        }

        #region Windows API

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        #endregion
    }
} 