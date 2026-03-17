using Dapr.DistributedLock.Extensions;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Dapr.Locker;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


public static class ModuleDaprLockerBuilderExtensions
{
    public static ModuleDaprLockerGuide UseDaprProvider(this ModuleLockerGuide guide,
        Action<ModuleDaprLockerOption>? action = null)
    {
        guide.SetDistributedLockProvider<DaprMoDistributedLock>();
        return new ModuleDaprLockerGuide().Register(action);
    }
}

public class ModuleDaprLocker(ModuleDaprLockerOption option)
    : MoModule<ModuleDaprLocker, ModuleDaprLockerOption, ModuleDaprLockerGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DaprLocker;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register DaprDistributedLockClient with configuration
        services.AddDaprDistributedLock((serviceProvider, clientBuilder) =>
        {
            // Apply custom HTTP endpoint if configured
            if (!string.IsNullOrWhiteSpace(option.DaprHttpEndpoint))
            {
                clientBuilder.UseHttpEndpoint(option.DaprHttpEndpoint);
            }

            // Apply custom API token if configured
            if (!string.IsNullOrWhiteSpace(option.DaprApiToken))
            {
                clientBuilder.UseDaprApiToken(option.DaprApiToken);
            }

            // Apply custom gRPC channel options if configured
            if (option.GrpcChannelOptions != null)
            {
                clientBuilder.UseGrpcChannelOptions(option.GrpcChannelOptions);
            }
        });
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLockerGuide>().Register();
    }
}

public class ModuleDaprLockerGuide : MoModuleGuide<ModuleDaprLocker, ModuleDaprLockerOption, ModuleDaprLockerGuide>
{
    

}

public class ModuleDaprLockerOption : MoModuleOption<ModuleDaprLocker>
{
    public string StoreName { get; set; } = default!;

    public string? OwnerPrefix { get; set; }

    public TimeSpan DefaultExpirationTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Custom HTTP endpoint for Dapr sidecar. Falls back to DAPR_HTTP_ENDPOINT environment variable if not specified.
    /// </summary>
    public string? DaprHttpEndpoint { get; set; }

    /// <summary>
    /// Custom gRPC endpoint for Dapr sidecar. Falls back to DAPR_GRPC_ENDPOINT environment variable if not specified.
    /// </summary>
    public string? DaprGrpcEndpoint { get; set; }

    /// <summary>
    /// API token for Dapr authentication. Falls back to DAPR_API_TOKEN environment variable if not specified.
    /// </summary>
    public string? DaprApiToken { get; set; }

    /// <summary>
    /// Custom gRPC channel options for advanced scenarios.
    /// </summary>
    public GrpcChannelOptions? GrpcChannelOptions { get; set; }
}
