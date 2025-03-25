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

    public int AddTask(string expression, Action action, DateTime? startAt = null, DateTime? endAt = null)
    {
        if (!IsValidExpression(expression)) throw new ArgumentException($"Crontab 表达式 \"{expression}\" 错误");

        var task = new ScheduledTask
        {
            Id = _nextTaskId++,
            Expression = expression,
            Action = action,
            StartAt = startAt ?? DateTime.Now,
            EndAt = endAt
        };

        _tasks.TryAdd(task.Id, task);
        return task.Id;
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
            if (IsInSchedule(now, schedule))
            {
                task.Action.Invoke();
                task.TotalExecutedTimes++;
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