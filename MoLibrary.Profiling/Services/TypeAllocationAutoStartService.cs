using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Profiling.Models;
using MoLibrary.Profiling.Modules;

namespace MoLibrary.Profiling.Services;

/// <summary>
///     自动启动类型分配收集的托管服务
/// </summary>
public class TypeAllocationAutoStartService(
    TypeAllocationCollector collector,
    IOptions<ModuleProfilingUIOption> options,
    ILogger<TypeAllocationAutoStartService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opt = options.Value;
        if (!opt.AutoStartCollection)
        {
            // 服务不应该被注册到这里，但为安全起见检查
            return Task.CompletedTask;
        }

        var mode = opt.DefaultSamplingMode;
        if (mode == AllocationSamplingMode.Disabled)
        {
            logger.LogWarning("AutoStartCollection 已启用但 DefaultSamplingMode 为 Disabled，跳过自动启动");
            return Task.CompletedTask;
        }

        logger.LogInformation("自动启动类型分配收集，模式: {Mode}", mode);
        collector.StartCollection(mode);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (collector.IsCollecting)
        {
            logger.LogInformation("正在停止类型分配收集");
            collector.StopCollection();
        }

        return Task.CompletedTask;
    }
}
