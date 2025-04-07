using System.Collections.Concurrent;
using System.Diagnostics;
using System.Dynamic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features;
using MoLibrary.RegisterCentre;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;
using MoLibrary.Tool.Utils;

namespace MoLibrary.Framework.Features;

public interface IMoTimekeeper
{
    /// <summary>
    /// 用于HTTP的自动结束的计时器
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public IDisposable CreateResAutoTimer(string key);

    /// <summary>
    /// 自动结束的计时器
    /// </summary>
    /// <param name="key"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    public AutoTimekeeper CreateAutoTimer(string key, string? content = null);

    /// <summary>
    /// 普通计时器，可以手动开始和结束
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public NormalTimekeeper CreateNormalTimer(string key);
}

public class MoTimekeeperManager(IHttpContextAccessor accessor, ILogger<MoTimekeeperManager> logger) : IMoTimekeeper
{
    public IDisposable CreateResAutoTimer(string key)
    {
        if (accessor.HttpContext?.GetOrNew<MoRequestContext>() is { } context)
        {
            return new ResAutoTimekeeper(context, key, logger);
        }

        return NullDisposable.Instance;
    }

    public AutoTimekeeper CreateAutoTimer(string key, string? content = null)
    {
        var keeper = new AutoTimekeeper(key, logger)
        {
            Content = content
        };
        return keeper;
    }

    public NormalTimekeeper CreateNormalTimer(string key)
    {
        return new NormalTimekeeper(key, logger);
    }
}



public abstract class MoTimekeeperBase(string key, ILogger logger) : IDisposable
{
    protected readonly string Key = key;
    protected readonly ILogger Logger = logger;
    protected readonly Stopwatch Timer = new();
    protected bool Disposed;
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
    public record TimekeeperMeasurement(string Name, long Duration)
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
    }

    private static readonly ConcurrentQueue<TimekeeperMeasurement> _queue = [];
    private static readonly Dictionary<string, TimekeeperStatistics> _recordDict = [];


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
                            LastDuration = info.Duration
                        };
                    }
                    else
                    {
                        _recordDict[info.Name] = new TimekeeperStatistics(1, info.Duration, DateTime.Now)
                        {
                            AverageMemoryBytes = info.MemoryBytes
                        };
                    }
                }
            }
           
        }, TaskCreationOptions.LongRunning);
    }

    public static IReadOnlyDictionary<string, TimekeeperStatistics> GetStatistics() => _recordDict;

    public TimekeeperStatistics? GetRecords(string key)
    {
        if (_recordDict.TryGetValue(key, out var value)) return value;
        return null;
    }
    public void ResetRecords(string key)
    {
        _recordDict.Remove(key);
    }
    private void DoRecord()
    {
        Check.NotNull(Key, nameof(Key));
        _queue.Enqueue(new TimekeeperMeasurement(Key, Timer.ElapsedMilliseconds)
        {
            MemoryBytes = MemoryUsage
        });
        if (EnableMemoryMonitor)
        {
            MemoryUsage = null;
        }
    }
    #endregion
    public virtual void Start()
    {
        Timer.Start();
        if (EnableMemoryMonitor)
        {
            MemoryUsage = GC.GetAllocatedBytesForCurrentThread();
        }
    }

    public virtual void Finish()
    {
        Timer.Stop();
        if (EnableMemoryMonitor)
        {
            MemoryUsage -= GC.GetAllocatedBytesForCurrentThread();
            if (MemoryUsage < 0)
            {
                MemoryUsage = null;
            }
        }
        DoRecord();
        Timer.Reset();
    }

    public virtual void Dispose()
    {
        Disposed = true;
    }
    /// <summary>
    /// 获取ElapsedMilliseconds，例：10ms
    /// </summary>
    /// <returns></returns>
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

public class ResAutoTimekeeper : MoTimekeeperBase
{
    private readonly MoRequestContext? _context;

    public ResAutoTimekeeper(MoRequestContext context, string key, ILogger logger) : base(key, logger)
    {
        _context = context;
        Start();
    }

    public override void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Finish();
        if (_context is not null)
        {
            _context.OtherInfo ??= new ExpandoObject();
            _context.OtherInfo.Append("timer", new { name = Key, duration = $"{Timer.ElapsedMilliseconds}ms" });
        }
        LoggingElapsedMs();
    }
}

public class AutoTimekeeper : MoTimekeeperBase
{
    public AutoTimekeeper(string key, ILogger logger) : base(key, logger) => Timer.Start();
    public override void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Finish();
        LoggingElapsedMs();
    }
}

public class NormalTimekeeper(string key, ILogger logger) : MoTimekeeperBase(key, logger)
{
    public override void Finish()
    {
        base.Finish();
        LoggingElapsedMs();
    }
}

public static class MoTimekeeperBuilderExtensions
{
    public static void AddMoTimekeeper(this IServiceCollection services)
    {
        services.AddSingleton<IMoTimekeeper, MoTimekeeperManager>();
    }
    /// <summary>
    /// 供注册中心使用
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsMoTimekeeperDashboard(this IApplicationBuilder app, string? groupName = "注册中心")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = groupName, Description = "Timekeeper基础功能" }
            };
            endpoints.MapGet("/centre/timekeeper/status", async (HttpResponse response, HttpContext context, [FromServices] IRegisterCentreServer centreServer) =>
            {
                var dict = await centreServer.GetAsync<object>("/timekeeper/status");
                return dict.Select(p => new
                {
                    p.Key.AppId,
                    p.Value
                });

            }).WithName("批量获取Timekeeper统计状态").WithOpenApi(operation =>
            {
                operation.Summary = "批量获取Timekeeper统计状态";
                operation.Description = "批量获取Timekeeper统计状态";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
    public static void UseEndpointsMoTimekeeper(this IApplicationBuilder app, string? groupName = "Timekeeper")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = groupName, Description = "Timekeeper基础功能" }
            };
            endpoints.MapGet("/timekeeper/status", async (HttpResponse response, HttpContext context) =>
            {
                var res = MoTimekeeperBase.GetStatistics();
                var list = res.OrderByDescending(p=>p.Value.Average).Select(p => new
                {
                    name = p.Key,
                    times = p.Value.Times,
                    average = $"{p.Value.Average:0.##}ms",
                    createAt = $"{p.Value.StartTime}",
                    timesEveryMinutes = $"{p.Value.Times / (DateTime.Now - p.Value.StartTime).TotalMinutes:0.##}",
                    averageMemory = $"{p.Value.AverageMemoryBytes?.FormatBytes()}",
                    lastMemory = $"{p.Value.LastMemoryBytes?.FormatBytes()}",
                    lastDuration = $"{p.Value.LastDuration:0.##}ms",
                    
                });
                await response.WriteAsJsonAsync(list);
            }).WithName("获取Timekeeper统计状态").WithOpenApi(operation =>
            {
                operation.Summary = "获取Timekeeper统计状态";
                operation.Description = "获取Timekeeper统计状态";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}