using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Interfaces;

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



public class MoConfigurationDefaultMemoryStore : IMoConfigurationStores
{
    private readonly List<DtoOptionHistory> _historyStore = new();
    private readonly object _lock = new();

    public async Task<Res> SaveUpdate(DtoUpdateConfigRes config)
    {
        await Task.CompletedTask;
        
        lock (_lock)
        {
            var lastVersion = _historyStore
                .Where(h => h.AppId == config.AppId && h.Key == config.Key)
                .Select(h => int.TryParse(h.Version, out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();
            
            var history = new DtoOptionHistory
            {
                Title = config.Title,
                AppId = config.AppId!,
                Key = config.Key,
                ModificationTime = DateTime.Now,
                OldValue = config.OldValue,
                NewValue = config.NewValue,
                Version = (lastVersion + 1).ToString()
            };
            
            _historyStore.Add(history);
        }
        
        return Res.Ok();
    }

    public async Task<Res<List<DtoOptionHistory>>> GetHistory(string key, string appid)
    {
        await Task.CompletedTask;
        
        lock (_lock)
        {
            var history = _historyStore
                .Where(h => h.AppId == appid && h.Key == key)
                .OrderByDescending(h => h.ModificationTime)
                .ToList();
            
            return history;
        }
    }

    public async Task<Res<List<DtoOptionHistory>>> GetHistory(DateTime start, DateTime end)
    {
        await Task.CompletedTask;
        
        lock (_lock)
        {
            var history = _historyStore
                .Where(h => h.ModificationTime >= start && h.ModificationTime <= end)
                .OrderByDescending(h => h.ModificationTime)
                .ToList();
            
            return history;
        }
    }

    public async Task<Res<DtoOptionHistory>> GetHistory(string key, string appid, string version)
    {
        await Task.CompletedTask;
        
        lock (_lock)
        {
            var history = _historyStore.FirstOrDefault(h => 
                h.AppId == appid && 
                h.Key == key && 
                h.Version == version);
            
            if (history == null)
                return Res.Fail("无对应配置类历史");
            
            return history;
        }
    }
}