using System.Collections.Generic;

namespace MoLibrary.Tool.Web.RateLimiter
{
    /// <summary>
    /// 限流模式
    /// </summary>
    public enum LimitingType
    {
        /// <summary>
        /// 令牌桶模式，适用于常有高数量突发请求的，即可能会有达到最大QPS的情况
        /// </summary>
        TokenBucket,
        /// <summary>
        /// 漏桶模式，不管多少请求处理速度都一定，比较平滑，不会达到最大QPS，但平均处理速率是基于QPS的
        /// </summary>
        LeakyBucket,
    }
    /// <summary>
    /// 限制了长度的限速器专用请求队列，多了则无法添加，且会返回false的加入队列错误的返回值。
    /// </summary>
    internal class LimitedQueue<T> : Queue<T>
    {
        public int Limit { get; set; }
        /// <summary>
        /// 可以对队列进行加锁
        /// </summary>
        internal readonly object QueueLock = new();
        /// <summary>
        /// 出队次数（可用于判断距离队头长度）
        /// </summary>
        public int DequeueTimes { get; set; }
        /// <summary>
        /// 创建一个限制长度的队列
        /// </summary>
        /// <param name="limit">0为不限制</param>
        public LimitedQueue(int limit) : base(limit)
        {
            Limit = limit;
        }
        /// <summary>
        /// 创建一个不限制长度的队列（实际上就是Queue）
        /// </summary>
        public LimitedQueue() : this(0)
        {

        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="item"></param>
        /// <returns>如果超过队列限制数则返回false</returns>
        public new bool Enqueue(T item)
        {
            if (Limit > 0 && Count >= Limit) return false;
            base.Enqueue(item);
            return true;
        }
        /// <summary>
        /// 元素出队
        /// </summary>
        /// <returns></returns>
        public new T Dequeue()
        {
            DequeueTimes++;
            return base.Dequeue();
        }
    }
}
