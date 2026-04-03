using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Repository.Snowflake.Abstractions;
using Monica.Repository.Snowflake.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSnowflakeBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the Snowflake ID generation module.
        /// </summary>
        public static ModuleSnowflakeGuide AddSnowflake(Action<ModuleSnowflakeOption>? action = null)
        {
            return new ModuleSnowflakeGuide().Register(action);
        }
    }
}

/// <summary>
/// Provides distributed Snowflake-based identifier generation.
/// </summary>
[ModuleKey(EMoModuleKey.Snowflake)]
public class ModuleSnowflake(ModuleSnowflakeOption option)
    : MoModule<ModuleSnowflake, ModuleSnowflakeOption, ModuleSnowflakeGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        var generator = new SnowflakeIdGenerator(option);
        SnowflakeStatic.Snowflake = generator;
        services.AddSingleton<ISnowflakeIdGenerator>(generator);
    }
}

public class ModuleSnowflakeGuide : MoModuleGuide<ModuleSnowflake, ModuleSnowflakeOption, ModuleSnowflakeGuide>
{
}

public class ModuleSnowflakeOption : MoModuleOption<ModuleSnowflake>
{
    /// <summary>
    /// Custom epoch start timestamp in milliseconds.
    /// The default value corresponds to 2015-01-01T00:00:00Z.
    /// </summary>
    public long Twepoch { get; set; } = 1420041600000L;

    /// <summary>
    /// Number of bits reserved for the worker identifier.
    /// Increase this only when you need more worker nodes and are willing to reduce timestamp or sequence capacity.
    /// </summary>
    public int WorkerIdBits { get; set; } = 5;

    /// <summary>
    /// Number of bits reserved for the datacenter identifier.
    /// Increase this only when you need more datacenter nodes and are willing to reduce timestamp or sequence capacity.
    /// </summary>
    public int DatacenterIdBits { get; set; } = 5;

    /// <summary>
    /// Number of bits reserved for the per-millisecond sequence.
    /// The default supports up to 4096 identifiers per node per millisecond.
    /// </summary>
    public int SequenceBits { get; set; } = 12;

    /// <summary>
    /// Worker identifier for the current generator instance.
    /// Keep this unique within the same datacenter to avoid collisions.
    /// </summary>
    public long WorkerId { get; set; } = 0L;

    /// <summary>
    /// Datacenter identifier for the current generator instance.
    /// Keep the datacenter and worker combination unique across deployments.
    /// </summary>
    public long DatacenterId { get; set; } = 0L;
}
