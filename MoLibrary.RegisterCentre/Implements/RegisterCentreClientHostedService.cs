using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;

public class RegisterCentreClientHostedService(
    IRegisterCentreClientInfo client,
    ILogger<RegisterCentreClientHostedService> logger,
    IOptions<ModuleRegisterCentreOption> option,
    IRegisterCentreServerConnector connector) : IHostedService
{
    protected readonly ModuleRegisterCentreOption Option = option.Value;
    private CancellationTokenSource? _heartbeatCts;

    protected virtual void StartHeartbeat()
    {
        // 取消之前的心跳任务
        _heartbeatCts?.Cancel();
        _heartbeatCts = new CancellationTokenSource();

        Task.Factory.StartNew(async () => await DoingHeartbeat(_heartbeatCts.Token), _heartbeatCts.Token,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    protected virtual async Task DoingHeartbeat(CancellationToken cancellationToken)
    {
        await Task.Delay(3000, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var serviceInfo = client.GetServiceStatus(true);
                var heartbeat = new ServiceHeartbeat
                {
                    AppId = serviceInfo.AppId,
                    BuildTime = serviceInfo.BuildTime,
                    AssemblyVersion = serviceInfo.AssemblyVersion,
                    ReleaseVersion = serviceInfo.ReleaseVersion,
                    FromClient = serviceInfo.FromInstance
                };
                
                if ((await connector.Heartbeat(heartbeat)).IsFailed(out var heartbeatError,
                        out var heartbeatData))
                {
                    logger?.LogError("心跳失败: {Message}", heartbeatError.Message);
                }
                else if (heartbeatData.RequireReRegister)
                {
                    logger?.LogInformation("需要重新注册: {Message}", heartbeatData.Message);
                    // 重新注册
                    var registerRes = await connector.Register(serviceInfo);
                    if (registerRes.IsFailed(out var registerError))
                    {
                        logger?.LogError("重新注册失败: {Message}", registerError.Message);
                    }
                    else
                    {
                        logger?.LogInformation("重新注册成功");
                    }
                }
            }
            catch (Exception e)
            {
                logger?.LogError(e, "向注册中心发送心跳出现异常");
            }
            finally
            {
                await Task.Delay(Option.HeartbeatDuration, cancellationToken);
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Factory.StartNew<Task>(async () =>
        {
            await Task.Delay(3000, cancellationToken);
            logger.LogInformation("开始注册到注册中心");
            var retryTimes = Option.ClientRetryTimes;

            while (retryTimes > 0)
            {
                try
                {
                    var serviceInfo = client.GetServiceStatus();
                    if ((await connector.Register(serviceInfo)).IsFailed(out var error))
                    {
                        logger?.LogError("注册失败: {Message}", error.Message);
                    }
                    else
                    {
                        logger?.LogInformation("成功注册到注册中心: {AppId}", serviceInfo.AppId);
                        StartHeartbeat();
                        break;
                    }
                }
                catch (Exception e)
                {
                    logger?.LogError(e, "注册配置中心出现异常");
                }
                finally
                {
                    await Task.Delay(Option.RetryDuration);
                    retryTimes--;
                }
            }

            if (retryTimes == 0)
            {
                logger?.LogError("注册中心注册失败，已达到最大重试次数");
            }
        }, cancellationToken);
    
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _heartbeatCts?.Cancel();
        return Task.CompletedTask;
    }
}