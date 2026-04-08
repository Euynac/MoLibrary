using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Core.Results;

namespace Monica.Configuration.Providers.History;

public class MemoryConfigurationHistoryStore : IConfigurationHistoryStore
{
    private readonly List<ConfigurationHistoryEntry> _historyStore = new();
    private readonly object _lock = new();

    public async Task<Res> SaveUpdate(ConfigurationUpdateResult config)
    {
        await Task.CompletedTask;

        lock (_lock)
        {
            var lastVersion = _historyStore
                .Where(h => h.AppId == config.AppId && h.Key == config.Key)
                .Select(h => int.TryParse(h.Version, out var version) ? version : 0)
                .DefaultIfEmpty(0)
                .Max();

            var history = new ConfigurationHistoryEntry
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

    public async Task<Res<List<ConfigurationHistoryEntry>>> GetHistory(string key, string appid)
    {
        await Task.CompletedTask;

        lock (_lock)
        {
            return _historyStore
                .Where(h => h.AppId == appid && h.Key == key)
                .OrderByDescending(h => h.ModificationTime)
                .ToList();
        }
    }

    public async Task<Res<List<ConfigurationHistoryEntry>>> GetHistory(DateTime start, DateTime end)
    {
        await Task.CompletedTask;

        lock (_lock)
        {
            return _historyStore
                .Where(h => h.ModificationTime >= start && h.ModificationTime <= end)
                .OrderByDescending(h => h.ModificationTime)
                .ToList();
        }
    }

    public async Task<Res<ConfigurationHistoryEntry>> GetHistory(string key, string appid, string version)
    {
        await Task.CompletedTask;

        lock (_lock)
        {
            var history = _historyStore.FirstOrDefault(h =>
                h.AppId == appid &&
                h.Key == key &&
                h.Version == version);

            if (history is null)
            {
                return Res.Fail("无对应配置类历史");
            }

            return history;
        }
    }
}
