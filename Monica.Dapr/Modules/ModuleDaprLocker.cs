using Dapr.DistributedLock.Extensions;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Dapr.Services;
using Monica.Locker.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprLockerBuilderExtensions
{
    /// <summary>
    /// Registers the Dapr lock provider on top of the Locker module and returns the Dapr-specific guide.
    /// </summary>
    /// <param name="guide">The locker guide that should use the Dapr provider.</param>
    /// <param name="action">Optional Dapr locker option configuration.</param>
    /// <returns>The Dapr locker guide.</returns>
    public static ModuleDaprLockerGuide UseDaprProvider(this ModuleLockerGuide guide,
        Action<ModuleDaprLockerOption>? action = null)
    {
        guide.UseProvider<DaprLockProvider>();
        return new ModuleDaprLockerGuide().Register(action);
    }
}

/// <summary>
/// Registers the Dapr distributed lock client integration used by <see cref="DaprLockProvider"/>.
/// </summary>
[ModuleKey(BuiltInModuleKey.DaprLocker)]
public class ModuleDaprLocker(ModuleDaprLockerOption option)
    : ModuleBase<ModuleDaprLocker, ModuleDaprLockerOption, ModuleDaprLockerGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Keep client customization here so provider code only focuses on acquisition semantics.
        services.AddDaprDistributedLock((serviceProvider, clientBuilder) =>
        {
            if (!string.IsNullOrWhiteSpace(option.DaprHttpEndpoint))
            {
                clientBuilder.UseHttpEndpoint(option.DaprHttpEndpoint);
            }

            if (!string.IsNullOrWhiteSpace(option.DaprApiToken))
            {
                clientBuilder.UseDaprApiToken(option.DaprApiToken);
            }

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

/// <summary>
/// Configures the Dapr-backed locker integration.
/// </summary>
public class ModuleDaprLockerGuide : ModuleGuide<ModuleDaprLocker, ModuleDaprLockerOption, ModuleDaprLockerGuide>
{
}

/// <summary>
/// Configures how Monica talks to Dapr when acquiring distributed locks.
/// </summary>
public class ModuleDaprLockerOption : ModuleOptions<ModuleDaprLocker>
{
    /// <summary>
    /// Name of the Dapr lock store component used by lock requests.
    /// </summary>
    public string StoreName { get; set; } = default!;

    /// <summary>
    /// Prefix added to auto-generated owner ids when a caller does not supply <see cref="LockAcquisitionOptions.Owner"/>.
    /// </summary>
    public string? LockOwnerPrefix { get; set; }

    /// <summary>
    /// Default lease duration used when <see cref="LockAcquisitionOptions.LeaseDuration"/> is not provided.
    /// </summary>
    public TimeSpan DefaultLeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Custom HTTP endpoint for the Dapr sidecar. When omitted, Dapr's default endpoint resolution still applies.
    /// </summary>
    public string? DaprHttpEndpoint { get; set; }

    /// <summary>
    /// Reserved for future explicit gRPC endpoint customization. The current registration path relies on Dapr's default
    /// endpoint resolution and optional <see cref="GrpcChannelOptions"/> overrides.
    /// </summary>
    public string? DaprGrpcEndpoint { get; set; }

    /// <summary>
    /// API token used for Dapr authentication. When omitted, Dapr's default token resolution still applies.
    /// </summary>
    public string? DaprApiToken { get; set; }

    /// <summary>
    /// Custom gRPC channel options applied to the distributed lock client.
    /// </summary>
    public GrpcChannelOptions? GrpcChannelOptions { get; set; }
}
