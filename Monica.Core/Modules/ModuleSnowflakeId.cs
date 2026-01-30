using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Features.MoSnowflake;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

namespace Monica.Core.Modules;


public static class ModuleSnowflakeIdBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 SnowflakeId 模块
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
    /// 开始时间截(2015-01-01)
    /// </summary>
    public long Twepoch { get; set; } = 1420041600000L;

    /// <summary>
    /// 机器id所占的位数
    /// </summary>
    public int WorkerIdBits { get; set; } = 5;

    /// <summary>
    /// 数据标识id所占的位数
    /// </summary>
    public int DatacenterIdBits { get; set; } = 5;

    /// <summary>
    /// 序列在id中占的位数(1ms内的并发数)
    /// </summary>
    public int SequenceBits { get; set; } = 12;

    /// <summary>
    /// 机器id
    /// </summary>
    public long WorkerId { get; set; } = 0L;

    /// <summary>
    /// 数据中心id
    /// </summary>
    public long DatacenterId { get; set; } = 0L;
}