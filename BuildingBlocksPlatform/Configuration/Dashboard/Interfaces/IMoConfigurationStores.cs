using BuildingBlocksPlatform.Configuration.Dashboard.Model;
using BuildingBlocksPlatform.SeedWork;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.Configuration.Dashboard.Interfaces;

public interface IMoConfigurationStores
{
    /// <summary>
    /// 保存此次变更记录
    /// </summary>
    /// <param name="config"></param>
    Task<Res> SaveUpdate(DtoUpdateConfigRes config);

    /// <summary>
    /// 获取指定Key的变更历史记录
    /// </summary>
    /// <returns></returns>
    Task<Res<List<DtoOptionHistory>>> GetHistory(string key, string appid);

    /// <summary>
    /// 获取指定范围内变更历史记录
    /// </summary>
    /// <returns></returns>
    Task<Res<List<DtoOptionHistory>>> GetHistory(DateTime start, DateTime end);

    /// <summary>
    /// 获取指定Key和version的变更历史记录
    /// </summary>
    /// <returns></returns>
    Task<Res<DtoOptionHistory>> GetHistory(string key, string appid, string version);
}



public class MoConfigurationDefaultStore : IMoConfigurationStores
{
    public async Task<Res> SaveUpdate(DtoUpdateConfigRes config)
    {
        return Res.Ok();
    }

    public async Task<Res<List<DtoOptionHistory>>> GetHistory(string key, string appid)
    {
        return new List<DtoOptionHistory>();
    }

    public async Task<Res<List<DtoOptionHistory>>> GetHistory(DateTime start, DateTime end)
    {
        return new List<DtoOptionHistory>();
    }

    public async Task<Res<DtoOptionHistory>> GetHistory(string key, string appid, string version)
    {
        return Res.Fail("无对应配置类历史");
    }
}