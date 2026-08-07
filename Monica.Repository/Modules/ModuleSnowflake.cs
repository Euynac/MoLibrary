using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Repository.Snowflake.Abstractions;
using Monica.Repository.Snowflake.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSnowflakeBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the Snowflake ID generation module.
        /// </summary>
        public ModuleRegistration<ModuleSnowflake, ModuleSnowflakeOption> AddSnowflake(Action<ModuleSnowflakeOption>? action = null)
        {
            return builder.AddModule<ModuleSnowflake, ModuleSnowflakeOption>(action);
        }
    }
}

/// <summary>
/// Provides distributed Snowflake-based identifier generation.
/// </summary>
public class ModuleSnowflake : MonicaModule<ModuleSnowflakeOption>
{
    public override void ConfigureServices(ModuleContext<ModuleSnowflakeOption> context)
    {
        var services = context.Services;
        var generator = new SnowflakeIdGenerator(Option);
        services.AddSingleton<ISnowflakeIdGenerator>(generator);
    }
}



public class ModuleSnowflakeOption : ModuleOptions<ModuleSnowflake>
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
