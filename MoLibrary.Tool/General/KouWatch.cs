using System;
using System.Diagnostics;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// KouWatch for test action invoke efficiency.
    /// </summary>
    public static class KouWatch
    {
        /// <summary>
        /// Use stopwatch to test the given action's elapsed milliseconds.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static long GetElapsedMilliseconds(Action action)
        {
            var watch = new Stopwatch();
            watch.Start();
            action.Invoke();
            watch.Stop();
            return watch.ElapsedMilliseconds;
        }
        /// <summary>
        /// Use stopwatch to test the given action's elapsed milliseconds.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static TimeSpan GetElapsedDuration(Action action) => 
            TimeSpan.FromMilliseconds(GetElapsedMilliseconds(action));
        /// <summary>
        /// Quick test invoke time.
        /// </summary>
        public static Stopwatch Start()
        {
            var watch = new Stopwatch();
            watch.Start();
            return watch;
        }

        /// <summary>
        /// Quick stop and get Stopwatch duration.
        /// </summary>
        /// <param name="watch"></param>
        /// <returns></returns>
        public static TimeSpan GetDuration(this Stopwatch watch)
        {
            watch.Stop();
            return watch.Elapsed;
        }

        /// <summary>
        /// Test and print the invoke-time of specific action.
        /// </summary>
        /// <param name="action">the specific action</param>
        public static void Start(Action action)
        {
            Console.WriteLine($"Doing action...");
            var watch = new Stopwatch();
            watch.Start();
            action.Invoke();
            watch.Stop();
            Console.WriteLine($"Action invoke time: {watch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Test and print the invoke-time of specific action.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="actionName">Action name, better for distinguishing</param>
        public static void Start(string actionName, Action action)
        {
            Console.WriteLine($"Doing \"{actionName}\" action...");
            var watch = new Stopwatch();
            watch.Start();
            action.Invoke();
            watch.Stop();
            Console.WriteLine($"\"{actionName}\" action invoke time: {watch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        ///  For a tuple of action name and action need amount of data, test and print the invoke-time of specific action.
        /// </summary>
        /// <param name="dataAmount">the amount of data give to action to invoke. (usually use in Take() method)</param>
        /// <param name="tuples"></param>
        public static void Start(int[] dataAmount, params (string, Action<int> actions)[] tuples)
        {
            foreach (var (actionName, action) in tuples)
            {
                Console.WriteLine($"Testing \"{actionName}\" action...");
                foreach (var amount in dataAmount)
                {
                    var watch = new Stopwatch();
                    watch.Start();
                    action.Invoke(amount);
                    watch.Stop();
                    Console.WriteLine($"For {amount} count of data, the invoke time: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        /// <summary>
        /// Two actions to test the advantages and disadvantages.
        /// Automatically start timing, end, output the execution time, and print the result of comparing the two actions.
        /// </summary>
        /// <param name="action1Name"></param>
        /// <param name="action1"></param>
        /// <param name="action2Name"></param>
        /// <param name="action2"></param>
        /// <param name="testTimes">Doing each two actions times</param>
        public static void Start(string action1Name, Action action1, string action2Name, Action action2, int testTimes = 1)
        {
            var watch = new Stopwatch();
            Console.WriteLine($"\"{action1Name}\" action doing {testTimes} times...");
            watch.Start();
            for (var i = 0; i < testTimes; i++)
            {
                action1.Invoke();
            }
            watch.Stop();
            Console.WriteLine($"\"{action1Name}\" action invoke time: {watch.ElapsedMilliseconds}ms");
            Console.WriteLine($"\"{action2Name}\" action doing {testTimes} times...");
            var watch2 = new Stopwatch();
            watch2.Start();
            for (var i = 0; i < testTimes; i++)
            {
                action2.Invoke();
            }
            watch2.Stop();
            Console.WriteLine($"\"{action2Name}\" action invoke time: {watch2.ElapsedMilliseconds}ms");

            var watch2Time = watch2.ElapsedMilliseconds.LimitInRange(1, null);
            var watch1Time = watch.ElapsedMilliseconds.LimitInRange(1, null);
            Console.WriteLine(watch2Time > watch1Time
                ? $"Efficiency comparision: \"{action1Name}\" action execute {(watch2Time / (double)watch1Time - 1):P3} faster than action \"{action2Name}\""
                : $"Efficiency comparision: \"{action2Name}\" action execute {(watch1Time / (double)watch2Time - 1):P3} faster than action \"{action1Name}\"");
        }
    }
}