using System.Collections.Concurrent;
using System.Diagnostics;

namespace BuildingBlocksPlatform.Features;

public class Timekeeper
{
    private readonly string _id;
    private static readonly ConcurrentDictionary<string, (int, double)> _recordDict = new();
    private readonly Stopwatch _watch = new();
    public Timekeeper(string id)
    {
        _id = id;
    }

    public static Timekeeper Create(string id)
    {
        _recordDict.TryAdd(id, (0, 0));
        return new Timekeeper(id);
    }

    public void Start()
    {
        _watch.Start();
    }

    public void Stop()
    {
        _watch.Stop();
        var curDuration = (double)_watch.ElapsedMilliseconds;
        _recordDict.AddOrUpdate(_id, (1, curDuration), (s, tuple) =>
        {
            var (times, averageTime) = tuple;
            var average = (averageTime * times + curDuration) / (times + 1);
            return (times + 1, average);
        });
        _watch.Reset();
    }

    public static (int, double)? GetRecords(string key)
    {
        if (_recordDict.TryGetValue(key, out var value)) return value;
        return null;
    }
}