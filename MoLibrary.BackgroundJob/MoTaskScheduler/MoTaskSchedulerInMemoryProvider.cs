using System.Collections.Concurrent;
using MoLibrary.Tool.Extensions;
using NCrontab;
using Timer = System.Timers.Timer;

namespace MoLibrary.BackgroundJob.MoTaskScheduler;

public class MoTaskSchedulerInMemoryProvider : IMoTaskScheduler
{
    private readonly ConcurrentDictionary<int, ScheduledTask> _tasks = new();
    private readonly Timer _timer;
    private int _nextTaskId = 1;

    public MoTaskSchedulerInMemoryProvider()
    {
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


         
            async Task Action()
            {
                try
                {
                    // 设置任务为运行状态
                    task.IsRunning = true;
                    // 使用Task.Run创建新线程运行任务，避免阻塞调度器线程
                    await Task.Run(task.Task);
                    // 增加执行次数
                    task.TotalExecutedTimes++;
                }
                catch (Exception e)
                {
                }
                finally
                {
                    // 确保任务完成后重置运行状态
                    task.IsRunning = false;
                }
            }
        }
    }

    private static bool IsValidExpression(string expression)
    {
        return CrontabSchedule.TryParse(expression, new CrontabSchedule.ParseOptions { IncludingSeconds = true }) != null;
    }

    private static bool IsInSchedule(DateTime time, CrontabSchedule schedule)
    {
        return time.RoundToSecond().Equals(schedule.GetNextOccurrence(time.AddSeconds(-1).RoundToSecond()));
    }
}