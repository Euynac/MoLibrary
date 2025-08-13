using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;

public abstract class MoRegisterCentreServerConnectorBase(
    IRegisterCentreClient client, 
    ILogger<MoRegisterCentreServerConnectorBase> logger, 
    IOptions<ModuleRegisterCentreOption> option) : IRegisterCentreServerConnector
{
    protected readonly ModuleRegisterCentreOption Option = option.Value;
    protected readonly IRegisterCentreClient Client = client;
    protected readonly ILogger<MoRegisterCentreServerConnectorBase> Logger = logger;
    
    private CancellationTokenSource? _heartbeatCts;
    
    public abstract Task<Res> Register(ServiceRegisterInfo req);
    public abstract Task<Res<ServiceHeartbeatResponse>> Heartbeat(ServiceHeartbeat req);
    
    public virtual async Task DoingRegister()
    {
        await Task.Delay(3000);
        var retryTimes = Option.ClientRetryTimes;
        
        while (retryTimes > 0)
        {
            try
            {
                var serviceInfo = Client.GetServiceStatus();
                var res = await Register(serviceInfo);
                
                if (res.IsFailed(out var error))
                {
                    Logger?.LogError("注册失败: {Message}", error.Message);
                }
                else
                {
                    Logger?.LogInformation("成功注册到注册中心: {AppId}", serviceInfo.AppId);
                    StartHeartbeat();
                    break;
                }
            }
            catch (Exception e)
            {
                Logger?.LogError(e, "注册配置中心出现异常");
            }
            finally
            {
                await Task.Delay(Option.RetryDuration);
                retryTimes--;
            }
        }
        
        if (retryTimes == 0)
        {
            Logger?.LogError("注册中心注册失败，已达到最大重试次数");
        }
    }
    
    protected virtual void StartHeartbeat()
    {
        // 取消之前的心跳任务
        _heartbeatCts?.Cancel();
        _heartbeatCts = new CancellationTokenSource();
        
        Task.Run(async () => await DoingHeartbeat(_heartbeatCts.Token));
    }
    
    protected virtual async Task DoingHeartbeat(CancellationToken cancellationToken)
    {
        await Task.Delay(3000, cancellationToken);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var serviceInfo = Client.GetServiceStatus(true);
                var heartbeat = new ServiceHeartbeat
                {
                    AppId = serviceInfo.AppId,
                    BuildTime = serviceInfo.BuildTime,
                    AssemblyVersion = serviceInfo.AssemblyVersion,
                    ReleaseVersion = serviceInfo.ReleaseVersion,
                    FromClient = serviceInfo.FromClient
                };
                
                var res = await Heartbeat(heartbeat);
                
                if (res.IsFailed(out var heartbeatError, out var heartbeatData))
                {
                    Logger?.LogError("心跳失败: {Message}", heartbeatError.Message);
                }
                else if (heartbeatData.RequireReRegister)
                {
                    Logger?.LogInformation("需要重新注册: {Message}", heartbeatData.Message);
                    // 重新注册
                    var registerRes = await Register(serviceInfo);
                    if (registerRes.IsFailed(out var registerError))
                    {
                        Logger?.LogError("重新注册失败: {Message}", registerError.Message);
                    }
                    else
                    {
                        Logger?.LogInformation("重新注册成功");
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.LogError(e, "向配置中心发送心跳出现异常");
            }
            finally
            {
                await Task.Delay(Option.HeartbeatDuration, cancellationToken);
            }
        }
    }
}