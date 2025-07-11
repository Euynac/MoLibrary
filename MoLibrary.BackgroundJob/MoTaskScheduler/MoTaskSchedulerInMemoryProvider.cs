using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.Extensions;
using NCrontab;
using Timer = System.Timers.Timer;

namespace MoLibrary.BackgroundJob.MoTaskScheduler;

/// <summary>
/// (Singleton) 内存任务调度器
/// </summary>
public class MoTaskSchedulerInMemoryProvider : IMoTaskScheduler
{
    private readonly ILogger<MoTaskSchedulerInMemoryProvider> _logger;
    private readonly ConcurrentDictionary<int, ScheduledTask> _tasks = new();
    private readonly Timer _timer;
    private int _nextTaskId = 1;
    private readonly SemaphoreSlim _threadPoolSemaphore;
    /// <summary>
    /// 最大并发任务数
    /// </summary>
    private readonly int _maxConcurrentTasks = 20;
    /// <summary>
    /// 信号量超时时间
    /// </summary>
    private readonly TimeSpan _semaphoreTimeout = TimeSpan.FromMinutes(30);

    public MoTaskSchedulerInMemoryProvider(ILogger<MoTaskSchedulerInMemoryProvider> logger)
    {
        _logger = logger;
        _threadPoolSemaphore = new SemaphoreSlim(_maxConcurrentTasks, _maxConcurrentTasks);
        _timer = new Timer(1000);
        _timer.AutoReset = true;
        _timer.Elapsed += (sender, args) => CheckTasks();
        _timer.Start();
    }

    public int AddTask(string expression, Func<Task> task, DateTime? startAt = null, DateTime? endAt = null,
        bool skipWhenPreviousIsRunning = false)
    {
        if (!IsValidExpression(expression)) throw new ArgumentException($"Crontab 表达式 \"{expression}\" 错误");

        var scheduledTask = new ScheduledTask
        {
            Id = _nextTaskId++,
            Expression = expression,
            Task = task,
            StartAt = startAt ?? DateTime.Now,
            EndAt = endAt,
            SkipWhenPreviousIsRunning = skipWhenPreviousIsRunning
        };

        _tasks.TryAdd(scheduledTask.Id, scheduledTask);
        return scheduledTask.Id;
    }

    public bool DeleteTask(int taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.IsEnabled = false;
            task.EndAt = DateTime.Now;
            return true;
        }

        return false;
    }

    public bool DisableTask(int taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.IsEnabled = false;
            return true;
        }

        return false;
    }

    public bool EnableTask(int taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.IsEnabled = true;
            return true;
        }

        return false;
    }

    public IEnumerable<ScheduledTask> GetAllTasks()
    {
        return _tasks.Values;
    }


    private void CheckTasks()
    {
        var now = DateTime.Now;
        foreach (var task in _tasks.Values.Where(t =>
                     t.IsEnabled && t.StartAt <= now && (!t.EndAt.HasValue || t.EndAt.Value >= now)))
        {
            var schedule = CrontabSchedule.Parse(task.Expression,new CrontabSchedule.ParseOptions { IncludingSeconds = true });
            if (!IsInSchedule(now, schedule)) continue;
            // 如果启用了跳过功能且任务正在运行，则跳过本次执行
            if (task is { SkipWhenPreviousIsRunning: true, IsRunning: true})
            {
                continue;
            }

            // 将任务提交到专用线程池执行
            _ = ExecuteTaskInThreadPool(task);
        }
    }

    private static bool IsValidExpression(string expression)
    {
        return CrontabSchedule.TryParse(expression, new CrontabSchedule.ParseOptions { IncludingSeconds = true }) != null;
    }

    private async Task ExecuteTaskInThreadPool(ScheduledTask scheduledTask)
    {
        // 等待获取线程池槽位，如果线程池满了会在这里排队
        var success = await _threadPoolSemaphore.WaitAsync(_semaphoreTimeout);
        if (!success)
        {
            _logger?.LogWarning($"Failed to acquire semaphore for task {scheduledTask.Id} within timeout {_semaphoreTimeout}");
            return;
        }
        try
        {
            // 在后台线程中执行任务
            _ = Task.Run(async () =>
            {
                try
                {
                    // 设置任务为运行状态
                    scheduledTask.IsRunning = true;
                    // 执行实际任务
                    await scheduledTask.Task();
                    // 增加执行次数
                    scheduledTask.TotalExecutedTimes++;
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, $"Task {scheduledTask.Id} execution failed");
                }
                finally
                {
                    // 确保任务完成后重置运行状态
                    scheduledTask.IsRunning = false;
                    // 释放线程池槽位
                    _threadPoolSemaphore.Release();
                }
            });
        }
        catch(Exception ex)
        {
            // 如果Task.Run失败，确保释放信号量
            _threadPoolSemaphore.Release();
            _logger?.LogError(ex, "Failed to start task execution in thread pool");
        }
    }

    private static bool IsInSchedule(DateTime time, CrontabSchedule schedule)
    {
        return time.RoundToSecond().Equals(schedule.GetNextOccurrence(time.AddSeconds(-1).RoundToSecond()));
    }
}