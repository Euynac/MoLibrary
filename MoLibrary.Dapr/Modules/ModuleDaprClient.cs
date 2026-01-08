using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprClientBuilderExtensions
{
    public static ModuleDaprClientGuide ConfigModuleDaprClient(this WebApplicationBuilder builder,
        Action<ModuleDaprClientOption>? action = null)
    {
        return new ModuleDaprClientGuide().Register(action);
    }
}

public class ModuleDaprClient(ModuleDaprClientOption option)
    : MoModuleWithDependencies<ModuleDaprClient, ModuleDaprClientOption, ModuleDaprClientGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprClient;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprClient(builder => builder.UseGrpcChannelOptions(new GrpcChannelOptions()
        {
            MaxReceiveMessageSize = Option.MaxReceiveMessageSize,
            MaxSendMessageSize = Option.MaxSendMessageSize,
            MaxRetryBufferSize = Option.MaxRetryBufferSize,
        }).UseJsonSerializationOptions(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions));

        // Register health coordinator (singleton implementing both interface and IHostedService)
        services.AddSingleton<HealthCheck.DaprSidecarHealthCoordinator>();
        services.AddSingleton<Interfaces.IDaprSidecarHealthCoordinator>(sp =>
            sp.GetRequiredService<HealthCheck.DaprSidecarHealthCoordinator>());
        services.AddHostedService(sp =>
            sp.GetRequiredService<HealthCheck.DaprSidecarHealthCoordinator>());
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleDaprGuide>().Register();
        DependsOnModule<ModuleHostedServiceGuide>().Register();
    }
}

public class ModuleDaprClientGuide : MoModuleGuide<ModuleDaprClient, ModuleDaprClientOption, ModuleDaprClientGuide>
{


}

public class ModuleDaprClientOption : MoModuleOption<ModuleDaprClient>
{

    /// <summary>
    /// Gets or sets the maximum message size in bytes that can be sent from the client. Attempting to send a message
    /// that exceeds the configured maximum message size results in an exception.
    /// <para>
    /// A <c>null</c> value removes the maximum message size limit. Defaults to <c>null</c>.
    /// </para>
    /// </summary>
    public int? MaxSendMessageSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum message size in bytes that can be received by the client. If the client receives a
    /// message that exceeds this limit, it throws an exception.
    /// <para>
    /// A <c>null</c> value removes the maximum message size limit. Defaults to 4,194,304 (4 MB).
    /// </para>
    /// </summary>
    public int? MaxReceiveMessageSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum buffer size in bytes that can be used to store sent messages when retrying
    /// or hedging calls. If the buffer limit is exceeded, then no more retry attempts are made and all
    /// hedging calls but one will be canceled. This limit is applied across all calls made using the channel.
    /// <para>
    /// Setting this value alone doesn't enable retries. Retries are enabled in the service config, which can be done
    /// using <see cref="P:Grpc.Net.Client.GrpcChannelOptions.ServiceConfig" />.
    /// </para>
    /// <para>
    /// A <c>null</c> value removes the maximum retry buffer size limit. Defaults to 16,777,216 (16 MB).
    /// </para>
    /// <para>
    /// Note: Experimental API that can change or be removed without any prior notice.
    /// </para>
    /// </summary>
    public long? MaxRetryBufferSize { get; set; } = 100 * 1024 * 1024;

    // Health Check Options

    /// <summary>
    /// Interval between periodic health checks after initial success.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan PeriodicCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of retry attempts for initial health check.
    /// Default: 10 attempts
    /// </summary>
    public int InitialRetryTimes { get; set; } = 10;

    /// <summary>
    /// Initial interval between retry attempts.
    /// Default: 2 seconds
    /// </summary>
    public TimeSpan InitialRetryInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum interval between retry attempts (for exponential backoff).
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan MaxRetryInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Exponential backoff multiplier for retry delays.
    /// Default: 1.5
    /// </summary>
    public double BackoffMultiplier { get; set; } = 1.5;

    /// <summary>
    /// Number of consecutive failures before marking as Degraded.
    /// Default: 2
    /// </summary>
    public int DegradedThreshold { get; set; } = 2;

    /// <summary>
    /// Number of consecutive failures before marking as Unhealthy.
    /// Default: 5
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 5;

    /// <summary>
    /// Threshold for consecutive health check failures before triggering fail-fast (if enabled).
    /// Applies to both initial startup retries and runtime periodic checks.
    /// Default: 10 consecutive failures
    /// </summary>
    public int FailFastThreshold { get; set; } = 10;

    /// <summary>
    /// Whether to trigger graceful application shutdown when Dapr sidecar becomes unavailable.
    /// When enabled and consecutive failures reach FailFastThreshold (default: 10):
    ///   During initial startup:
    ///     - Status becomes DaprHealthStatus.Failed
    ///     - IHostApplicationLifetime.StopApplication() is called immediately
    ///   During runtime (periodic checks):
    ///     - After 10 consecutive periodic check failures
    ///     - IHostApplicationLifetime.StopApplication() is called
    ///   Result:
    ///     - Application exits gracefully (exit code 0)
    ///     - Kubernetes detects exit and recreates the pod
    /// Default: false (degrade gracefully, app continues in degraded mode)
    /// </summary>
    public bool EnableFailFast { get; set; } = false;
}