using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Monica.Tool.Utils;

namespace Monica.Core.Features.MoTimekeeper;

public abstract class MoTimekeeperBase(string key, ILogger logger) : IDisposable
{
    protected readonly string Key = key;
    protected readonly ILogger Logger = logger;
    protected readonly Stopwatch Timer = new();
    protected bool Disposed;
    protected bool IsFinished;
    /// <summary>
    /// Indicates the timekeeper finished via <see cref="Dispose" /> instead of a normal completion path.
    /// </summary>
    protected bool IsNotNormal;
    public bool EnableLogging { get; set; }

    /// <summary>
    /// Whether to monitor memory usage
    /// </summary>
    [Obsolete("暂时无法实现，目前无法确保异步方法线程ID不变")]
    public bool EnableMemoryMonitor { get; set; } = false;
    /// <summary>
    /// number of memory usage bytes of current measurement scope.
    /// </summary>
    public long? MemoryUsage { get; set; }

    public string? Content { get; set; }

    #region Statistic

    /// <summary>
    /// Record for storing timekeeper measurement data
    /// </summary>
    /// <param name="Name">The name/key of the timekeeper</param>
    /// <param name="Duration">The measured duration in milliseconds</param>
    /// <param name="ExecutedTime">The time when the measurement was executed</param>
    public record TimekeeperMeasurement(string Name, long Duration, DateTime ExecutedTime)
    {
        public long? MemoryBytes { get; set; }
    }

    /// <summary>
    /// Record for storing timekeeper statistics
    /// </summary>
    /// <param name="Times">Number of times the timekeeper has been used</param>
    /// <param name="Average">Average duration in milliseconds</param>
    /// <param name="StartTime">Time when the first measurement was recorded</param>
    public record TimekeeperStatistics(int Times, double Average, DateTime StartTime)
    {
        public long? AverageMemoryBytes { get; set; }
        public long? LastMemoryBytes { get; set; }
        public long? LastDuration { get; set; }
        public DateTime? LastExecutedTime { get; set; }
    }

    /// <summary>
    /// Record for storing currently running timekeeper information
    /// </summary>
    /// <param name="Key">The key/name of the timekeeper</param>
    /// <param name="StartTime">When the timekeeper was started</param>
    /// <param name="Content">Optional content description</param>
    public record RunningTimekeeperInfo(string Key, DateTime StartTime, string? Content)
    {
        /// <summary>
        /// Current elapsed time in milliseconds
        /// </summary>
        public long CurrentElapsedMs => (long)(DateTime.Now - StartTime).TotalMilliseconds;
    }

    private static readonly ConcurrentQueue<TimekeeperMeasurement> _queue = [];
    private static readonly Dictionary<string, TimekeeperStatistics> _recordDict = [];
    private static readonly ConcurrentDictionary<string, RunningTimekeeperInfo> _runningTimekeepers = [];

    static MoTimekeeperBase()
    {
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);
                while (_queue.TryDequeue(out var info))
                {
                    if (_recordDict.TryGetValue(info.Name, out var cur))
                    {
                        var average = (cur.Average * cur.Times + info.Duration) / (cur.Times + 1);
                        var averageMemory = info.MemoryBytes is { } memory
                            ? ((cur.AverageMemoryBytes * cur.Times ?? 0) + info.MemoryBytes) / (cur.Times + 1)
                            : cur.AverageMemoryBytes;
                        
                        _recordDict[info.Name] = cur with
                        {
                            Times = cur.Times + 1, Average = average, AverageMemoryBytes = averageMemory,
                            LastMemoryBytes = info.MemoryBytes,
                            LastDuration = info.Duration,
                            LastExecutedTime = info.ExecutedTime
                        };
                    }
                    else
                    {
                        _recordDict[info.Name] = new TimekeeperStatistics(1, info.Duration, DateTime.Now)
                        {
                            AverageMemoryBytes = info.MemoryBytes,
                            LastDuration = info.Duration,
                            LastExecutedTime = DateTime.Now
                        };
                    }
                }
            }
           
        }, TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Get completed timekeeper statistics
    /// </summary>
    /// <returns>Dictionary of completed timekeeper statistics</returns>
    public static IReadOnlyDictionary<string, TimekeeperStatistics> GetStatistics() => _recordDict;

    /// <summary>
    /// Get currently running timekeeper information
    /// </summary>
    /// <returns>Dictionary of currently running timekeeper information</returns>
    public static IReadOnlyDictionary<string, RunningTimekeeperInfo> GetRunningTimekeepers() => _runningTimekeepers;

    public TimekeeperStatistics? GetRecords(string key)
    {
        return _recordDict.GetValueOrDefault(key);
    }
    public void ResetRecords(string key)
    {
        _recordDict.Remove(key);
    }
    private void DoRecord()
    {
        Check.NotNull(Key, nameof(Key));
        _queue.Enqueue(new TimekeeperMeasurement(Key, Timer.ElapsedMilliseconds, DateTime.Now)
        {
            MemoryBytes = MemoryUsage
        });
#pragma warning disable CS0618 // Type or member is obsolete
        if (EnableMemoryMonitor)
#pragma warning restore CS0618
        {
            MemoryUsage = null;
        }
    }
    #endregion

    public virtual void Start()
    {
        Timer.Start();
        
        // Add to running timekeepers tracking
        if (_runningTimekeepers.TryGetValue(Key, out var old))
        {
            _runningTimekeepers.TryUpdate(Key, new RunningTimekeeperInfo(Key, DateTime.Now, Content), old);
        }
        else
        {
            _runningTimekeepers.TryAdd(Key, new RunningTimekeeperInfo(Key, DateTime.Now, Content));
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (EnableMemoryMonitor)
#pragma warning restore CS0618
        {
            MemoryUsage = GC.GetAllocatedBytesForCurrentThread();
        }
    }

    public virtual void Finish()
    {
        if(IsFinished) return;
        Timer.Stop();
        
        // Remove from running timekeepers tracking
        _runningTimekeepers.TryRemove(Key, out _);

#pragma warning disable CS0618 // Type or member is obsolete
        if (EnableMemoryMonitor)
#pragma warning restore CS0618
        {
            MemoryUsage -= GC.GetAllocatedBytesForCurrentThread();
            if (MemoryUsage < 0)
            {
                MemoryUsage = null;
            }
        }
        DoRecord();
        Timer.Reset();
        IsFinished = true;
    }

    public virtual void Dispose()
    {
        Disposed = true;
        if (!IsFinished)
        {
            IsNotNormal = true;
            Finish();
        }
    }

    /// <summary>
    /// Gets <see cref="Stopwatch.ElapsedMilliseconds" /> formatted as text, for example <c>10ms</c>.
    /// </summary>
    /// <returns>The formatted elapsed time.</returns>
    public string GetElapsedMs()
    {
        return $"{Timer.ElapsedMilliseconds}ms";
    }

    public void LoggingElapsedMs()
    {
        if (EnableLogging)
        {
            Logger.LogInformation("{name} cost time: {time}", Content ?? Key, GetElapsedMs());
        }
    }
}
