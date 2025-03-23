using System;
using System.Timers;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// Timer for task.
    /// </summary>
    public static class KouTaskTimer
    {
        /// <summary>
        /// Do given action every given time elapsed.
        /// </summary>
        /// <param name="interval">The time, in milliseconds, between events. The value must be greater than zero and less than or equal to <see cref="F:System.Int32.MaxValue" />.</param>
        /// <param name="action"></param>
        public static void DoWhenElapsed(double interval, Action action)
        {
            var timer = new Timer(interval);
            timer.Elapsed += (sender, args) => action();
            timer.AutoReset = true;
            timer.Start();
        }
    }
}
