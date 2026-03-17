using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Features.MoSnowflake;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


public static class ModuleSnowflakeIdBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the SnowflakeId module.
        /// </summary>
        public static ModuleSnowflakeIdGuide AddSnowflakeId(Action<ModuleSnowflakeIdOption>? action = null)
        {
            return new ModuleSnowflakeIdGuide().Register(action);
        }
    }
}

public class ModuleSnowflakeId(ModuleSnowflakeIdOption option)
    : MoModule<ModuleSnowflakeId, ModuleSnowflakeIdOption, ModuleSnowflakeIdGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.SnowflakeId;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        var generator = new DefaultSingletonSnowflakeGenerator(option);
        services.AddSingleton<ISnowflakeGenerator>(generator);
    }
}

public class ModuleSnowflakeIdGuide : MoModuleGuide<ModuleSnowflakeId, ModuleSnowflakeIdOption, ModuleSnowflakeIdGuide>
{


}
public class ModuleSnowflakeIdOption : MoModuleOption<ModuleSnowflakeId>
{
    /// <summary>
    /// Epoch start timestamp (2015-01-01).
    /// </summary>
    public long Twepoch { get; set; } = 1420041600000L;

    /// <summary>
    /// Number of bits reserved for the worker id.
    /// </summary>
    public int WorkerIdBits { get; set; } = 5;

    /// <summary>
    /// Number of bits reserved for the datacenter id.
    /// </summary>
    public int DatacenterIdBits { get; set; } = 5;

    /// <summary>
    /// Number of bits reserved for the per-millisecond sequence.
    /// </summary>
    public int SequenceBits { get; set; } = 12;

    /// <summary>
    /// Worker id.
    /// </summary>
    public long WorkerId { get; set; } = 0L;

    /// <summary>
    /// Datacenter id.
    /// </summary>
    public long DatacenterId { get; set; } = 0L;
}
