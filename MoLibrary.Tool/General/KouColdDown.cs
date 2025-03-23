using System;
using System.Collections.Concurrent;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// A cold down pool for a serials of operators to invoke a service or feature.
    /// This is not for API cool down but for one time easy operate cd,
    /// advanced usage please use LeakyBucketRateLimiter.
    /// To use it, you need to add this cold down pool to your class and mark as singleton (or static).
    /// T is the id type for distinguishing different operator (must support hash set).
    /// </summary>
    public class KouColdDown<T>
    {
        /// <summary>
        /// [operate ID, last operation invoke time] 
        /// </summary>
        private readonly ConcurrentDictionary<T, InvokeTime> _coldDownDict = new();
        /// <summary>
        /// For lock
        /// </summary>
        private class InvokeTime
        {
            private DateTime? _thePreviousTimeToInvoke = null;
            public void Invoke(TimeSpan nextCoolDownTime) => _thePreviousTimeToInvoke = DateTime.Now + nextCoolDownTime;
            public bool IsInTheCd() => _thePreviousTimeToInvoke > DateTime.Now;

            public TimeSpan? RemainingCd()
            {
                if (_thePreviousTimeToInvoke == null) return null;
                var remain = _thePreviousTimeToInvoke.Value - DateTime.Now;
                if (remain.TotalMilliseconds <= 0) return null;
                return remain;
            }

            public void ResetCd() => _thePreviousTimeToInvoke = null;
        }

        /// <summary>
        /// Test whether the operation is in the cool down time. (The same as !IsOK())
        /// </summary>
        /// <returns>if true, it will think the operate is going to invoke and the cool down will start, or it will give
        /// the remaining cd duration.</returns>
        public bool IsInCd(T operatorID, TimeSpan coolDownTime, out TimeSpan remainingDuration) =>
            !IsOk(operatorID, coolDownTime, out remainingDuration);
        /// <summary>
        /// Test whether the operation is in the cool down time. (The same as !IsOK())
        /// </summary>
        /// <returns>if false, it will think the operate is going to invoke and the cool down will start</returns>
        public bool IsInCd(T operatorID, TimeSpan coolDownTime) => !IsOk(operatorID, coolDownTime);
        /// <summary>
        /// Test whether the operation has passed the cool down time.
        /// </summary>
        /// <returns>if true, it will think the operate is going to invoke and the cool down will start</returns>
        public bool IsOk(T operatorID, TimeSpan coolDownTime)
        {
            var tester = _coldDownDict.GetOrAdd(operatorID, new InvokeTime());
            if (tester.IsInTheCd()) return false;
            lock (tester)
            {
                if (tester.IsInTheCd()) return false;
                tester.Invoke(coolDownTime);
            }
            return true;
        }
        /// <summary>
        /// Reset cd of specific operator.
        /// </summary>
        /// <param name="operatorID"></param>
        public void ResetCd(T operatorID)
        {
            var tester = _coldDownDict.GetOrAdd(operatorID, new InvokeTime());
            tester.ResetCd();
        }
        /// <summary>
        /// Reset cd of specific operator and get canceled duration.
        /// </summary>
        /// <param name="operatorID"></param>
        /// <param name="canceledDuration">if has no cd, return default(TimeSpan)</param>
        public void ResetCd(T operatorID, out TimeSpan canceledDuration)
        {
            canceledDuration = default;
            var tester = _coldDownDict.GetOrAdd(operatorID, new InvokeTime());
            var remainingCd = tester.RemainingCd();
            if (remainingCd == null) return;
            lock (tester)
            {
                remainingCd = tester.RemainingCd();
                if (remainingCd == null) return;
                canceledDuration = remainingCd.Value;
                tester.ResetCd();
            }
        }

        /// <summary>
        /// Test whether the operation has passed the cool down time.
        /// </summary>
        /// <returns>if true, it will think the operate is going to invoke and the cool down will start, or it will give
        /// the remaining cd duration.</returns>
        public bool IsOk(T operatorID, TimeSpan coolDownTime, out TimeSpan remainingDuration)
        {
            remainingDuration = TimeSpan.Zero;
            var tester = _coldDownDict.GetOrAdd(operatorID, new InvokeTime());
            var remainingCd = tester.RemainingCd();
            if (remainingCd != null)
            {
                remainingDuration = remainingCd.Value;
                return false;
            }
            lock (tester)
            {
                remainingCd = tester.RemainingCd();
                if (remainingCd != null)
                {
                    remainingDuration = remainingCd.Value;
                    return false;
                }
                tester.Invoke(coolDownTime);
            }
            return true;
        }
    }
}