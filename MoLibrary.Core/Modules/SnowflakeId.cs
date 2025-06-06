using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoSnowflake;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;


public static class ModuleSnowflakeIdBuilderExtensions
{
    public static ModuleSnowflakeIdGuide ConfigModuleSnowflakeId(this WebApplicationBuilder builder,
        Action<ModuleSnowflakeIdOption>? action = null)
    {
        return new ModuleSnowflakeIdGuide().Register(action);
    }
}

public class ModuleSnowflakeId(ModuleSnowflakeIdOption option)
    : MoModule<ModuleSnowflakeId, ModuleSnowflakeIdOption, ModuleSnowflakeIdGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SnowflakeId;
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