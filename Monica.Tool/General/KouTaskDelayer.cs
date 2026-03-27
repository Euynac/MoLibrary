using Monica.Tool.Extensions;

namespace Monica.Tool.General;

/// <summary>
/// Mouth timer, delayer
/// </summary>
public static class KouTaskDelayer
{
    private static readonly SortedList<DateTime, Task> _sleepTaskList = new(new DuplicateKeyComparer<DateTime>());
    // private static readonly SortedList<Tuple<DateTime, DateTime>, Task> _sleepTaskList = new(new DateTimeTupleComparer());
    private static readonly object _listLock = new();

    /// <summary>
    /// Single thread waiting time (ms)
    /// </summary>
    private const int SleepTime = 1000;

    static KouTaskDelayer()
    {
        StartTick();
    }
    /// <summary>
    /// Start timer
    /// </summary>
    private static void StartTick()
    {
        Task.Factory.StartNew(() =>
        {
            var canStartTask = false;
            while (true)
            {
                if (_sleepTaskList.Count <= 0) Thread.Sleep(1000);
                // KeyValuePair<Tuple<DateTime, DateTime>, Task> pair;
                KeyValuePair<DateTime, Task> pair;
                lock (_listLock)
                {
                    if (_sleepTaskList.Count <= 0) continue;
                    pair = _sleepTaskList.ElementAt(0);
                    if (pair.Key <= DateTime.Now)
                    {
                        _sleepTaskList.RemoveAt(0);
                        canStartTask = true;
                    }
                }
                if (canStartTask)//任务使用线程池中的线程完成，除非使用RunSynchronously
                {
                    pair.Value.Start();
                    canStartTask = false;
                }
                Thread.Sleep(SleepTime);
            }
        }, TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Completes action after a specified number of milliseconds.
    /// </summary>
    /// <param name="milliseconds"></param>
    /// <param name="action"></param>
    public static void DelayInvoke(int milliseconds, Action action)
    {
        Task.Factory.StartNew(async () =>
        {
            await Task.Delay(milliseconds);
            action.Invoke();
        });
    }

    /// <summary>
    /// Add tasks that need to be executed to the timing pool
    /// </summary>
    /// <param name="executeTime"></param>
    /// <param name="task">tasks to be performed</param>
    public static void AddTask(DateTime executeTime, Task task)
    {
        lock (_listLock)
        {
            // _sleepTaskList.Add(new Tuple<DateTime, DateTime>(executeTime, DateTime.Now), task);
            _sleepTaskList.Add(executeTime, task);
            //Allow repeated execution times, but note that methods such as Remove are invalid
        }
    }
    /// <summary>
    /// Add tasks that need to be executed to the timing pool
    /// </summary>
    /// <param name="executeTime"></param>
    /// <param name="action">tasks to be performed</param>
    public static void AddTask(DateTime executeTime, Action action)
    {
        lock (_listLock)
        {
            // _sleepTaskList.Add(new Tuple<DateTime, DateTime>(executeTime, DateTime.Now), new Task(action));
            _sleepTaskList.Add(executeTime, new Task(action));
            //Allow repeated execution times, but note that methods such as Remove are invalid
        }
    }

}

// /// <summary>
// /// The tuple first datetime means the time the task to start, and the second datetime means the task requirement time.
// /// </summary>
// internal class DateTimeTupleComparer : IComparer<Tuple<DateTime,DateTime>>
// {
//     [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
//     public int Compare(Tuple<DateTime,DateTime> x, Tuple<DateTime,DateTime> y )
//     {
//         if (x.Item1 == y.Item1)
//         {
//             if (x.Item2 == y.Item2) return -1;//won't be the same. for duplicate keys.
//             return x.Item2.CompareTo(y.Item2);
//         }
//         return x.Item1.CompareTo(y.Item1);
//     }
// }