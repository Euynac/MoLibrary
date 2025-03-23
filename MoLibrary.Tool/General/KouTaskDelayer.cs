using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// Kou定时、延时器
    /// </summary>
    public static class KouTaskDelayer
    {
        private static readonly SortedList<DateTime, Task> _sleepTaskList = new(new DuplicateKeyComparer<DateTime>());
        // private static readonly SortedList<Tuple<DateTime, DateTime>, Task> _sleepTaskList = new(new DateTimeTupleComparer());
        private static readonly object _listLock = new();

        /// <summary>
        /// 单次线程等待时间（ms）
        /// </summary>
        private const int SleepTime = 1000;

        static KouTaskDelayer()
        {
            StartTick();
        }
        /// <summary>
        /// 开启定时器
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
        /// 向定时池增加需要执行的任务
        /// </summary>
        /// <param name="executeTime"></param>
        /// <param name="task">要执行的任务</param>
        public static void AddTask(DateTime executeTime, Task task)
        {
            lock (_listLock)
            {
                // _sleepTaskList.Add(new Tuple<DateTime, DateTime>(executeTime, DateTime.Now), task);
                _sleepTaskList.Add(executeTime, task);
                //允许重复的执行时间，但注意Remove之类的方法失效
            }
        }
        /// <summary>
        /// 向定时池增加需要执行的任务
        /// </summary>
        /// <param name="executeTime"></param>
        /// <param name="action">要执行的任务</param>
        public static void AddTask(DateTime executeTime, Action action)
        {
            lock (_listLock)
            {
                // _sleepTaskList.Add(new Tuple<DateTime, DateTime>(executeTime, DateTime.Now), new Task(action));
                _sleepTaskList.Add(executeTime, new Task(action));
                //允许重复的执行时间，但注意Remove之类的方法失效
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


    /// <summary>
    /// Comparer for comparing two keys, handling equality as being lower (first come first out) Note: this will break Remove(key) or IndexOfKey(key) since the comparer never returns 0 to signal key equality
    /// Use this Comparer e.g. with SortedLists or SortedDictionaries, that don't allow duplicate keys
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    internal class DuplicateKeyComparer<TKey>
        :
            IComparer<TKey> where TKey : IComparable
    {
        #region IComparer<TKey> Members

        public int Compare(TKey x, TKey y)
        {
            var result = x.CompareTo(y);
            return result == 0 ? -1 : result;
        }

        #endregion
    }
}
