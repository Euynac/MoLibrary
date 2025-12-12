using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.HostedServices;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;

public class RegisterCentreClientHostedService(
    IRegisterCentreClientInfo client,
    ILogger<RegisterCentreClientHostedService> logger,
    IOptions<ModuleRegisterCentreOption> option,
    IRegisterCentreServerConnector connector,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger), IServiceRegistrationCoordinator
{
    protected readonly ModuleRegisterCentreOption Option = option.Value;
    private readonly TaskCompletionSource<bool> _registrationCompletionSource = new();
    private RegistrationStatus _status = RegistrationStatus.NotStarted;
    private readonly object _statusLock = new();

    public override string ServiceName => "RegisterCentreClient";

    public override TimeSpan? HeartbeatInterval => null; // Custom heartbeat management

    public RegistrationStatus Status
    {
        get
        {
            lock (_statusLock)
            {
                return _status;
            }
        }
        private set
        {
            lock (_statusLock)
            {
                _status = value;
            }
        }
    }

    public bool IsRegistered => Status == RegistrationStatus.Completed;

    public async Task<bool> WaitForRegistrationAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await _registrationCompletionSource.Task.WaitAsync(cts.Token);
            return _registrationCompletionSource.Task.Result;
        }
        catch (OperationCanceledException)
        {
            logger?.LogWarning("等待注册中心注册完成超时 ({Timeout})", timeout);
            return false;
        }
    }

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        // Phase 1: Registration with fixed-interval retry
        await RegisterWithRetryAsync(stoppingToken);

        // Phase 2: Continuous heartbeat loop (only if registration succeeded)
        if (Status == RegistrationStatus.Completed)
        {
            await SendHeartbeatsAsync(stoppingToken);
        }
    }

    private async Task RegisterWithRetryAsync(CancellationToken stoppingToken)
    {
        RecordState("开始注册到注册中心", HostedServiceState.Starting);
        Status = RegistrationStatus.InProgress;

        var retryCount = Option.ClientRetryTimes;
        var retryInterval = TimeSpan.FromSeconds(Option.InitialRetryInterval);
        var isInfiniteRetry = retryCount == 0;
        var attemptNumber = 0;

        while ((isInfiniteRetry || retryCount > 0) && !stoppingToken.IsCancellationRequested)
        {
            attemptNumber++;
            try
            {
                var serviceInfo = client.GetServiceStatus();
                if ((await connector.Register(serviceInfo)).IsFailed(out var error))
                {
                    var totalText = isInfiniteRetry ? "∞" : Option.ClientRetryTimes.ToString();
                    RecordState($"注册失败 (尝试 {attemptNumber}/{totalText}): {error.Message}", HostedServiceState.Degraded);
                }
                else
                {
                    Status = RegistrationStatus.Completed;
                    _registrationCompletionSource.TrySetResult(true);
                    RecordState("注册成功", HostedServiceState.Running);
                    return;
                }
            }
            catch (Exception ex)
            {
                var totalText = isInfiniteRetry ? "∞" : Option.ClientRetryTimes.ToString();
                RecordState($"注册异常 (尝试 {attemptNumber}/{totalText}): {ex.Message}", HostedServiceState.Degraded);
            }

            if (!isInfiniteRetry)
            {
                retryCount--;
            }

            if (isInfiniteRetry || retryCount > 0)
            {
                await Task.Delay(retryInterval, stoppingToken);
            }
        }

        // Registration failed (only reachable when not infinite retry)
        Status = RegistrationStatus.Failed;
        _registrationCompletionSource.TrySetResult(false);
        RecordState($"注册失败，已重试 {Option.ClientRetryTimes} 次", HostedServiceState.Faulted);
    }

    private async Task SendHeartbeatsAsync(CancellationToken stoppingToken)
    {
        // Initial delay before first heartbeat
        await Task.Delay(Option.HeartbeatInterval, stoppingToken);

        var heartbeatInterval = TimeSpan.FromSeconds(Option.HeartbeatInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RecordState("发送心跳", HostedServiceState.Executing);

                var serviceInfo = client.GetServiceStatus(true);
                var heartbeat = new ServiceHeartbeat
                {
                    AppId = serviceInfo.AppId,
                    BuildTime = serviceInfo.BuildTime,
                    AssemblyVersion = serviceInfo.AssemblyVersion,
                    ReleaseVersion = serviceInfo.ReleaseVersion,
                    FromClient = serviceInfo.FromInstance
                };

                if ((await connector.Heartbeat(heartbeat)).IsFailed(out var error, out var data))
                {
                    RecordState($"心跳失败: {error.Message}", HostedServiceState.Degraded);
                }
                else if (data.RequireReRegister)
                {
                    RecordState("需要重新注册", HostedServiceState.Degraded);

                    // Re-register with single attempt
                    var fullServiceInfo = client.GetServiceStatus();
                    var registerRes = await connector.Register(fullServiceInfo);
                    if (registerRes.IsFailed(out var registerError))
                    {
                        RecordState($"重新注册失败: {registerError.Message}", HostedServiceState.Degraded);
                    }
                    else
                    {
                        RecordState("重新注册成功", HostedServiceState.Running);
                    }
                }
                else
                {
                    RecordState("心跳成功", HostedServiceState.Running);
                }
            }
            catch (Exception ex)
            {
                RecordState("心跳异常", HostedServiceState.Degraded, ex);
            }
            finally
            {
                await Task.Delay(heartbeatInterval, stoppingToken);
            }
        }
    }
}