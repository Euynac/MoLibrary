using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Interfaces;

namespace MoLibrary.Tool.Web.RateLimiter
{
    /// <summary>
    /// 漏桶算法限流器
    /// </summary>
    public class LeakyBucketRateLimiter : IDisposable, IKouErrorMsg
    {
        /// <summary>
        /// 同一个api请求在同一个限速器队列中 [API名，请求队列]
        /// </summary>
        private static readonly Dictionary<string, LimitedQueue<LeakyBucketRateLimiter>> _limiterDictionary =
            new();

        private readonly string _limiterID;
        private static readonly object _dictLock = new();
        /// <summary>
        /// 该限速器上绑定的任务是否被处理，完成才可取出队列
        /// </summary>
        private bool _hasHandled;
        /// <summary>
        /// 完成任务的时间。当从队列移除时，会根据此时间+漏桶平均速率时间（根据QPS计算）来判断是否可移除
        /// </summary>
        private DateTime _handledTime;
        /// <summary>
        /// 判断是否可以从队列中移除
        /// </summary>
        private bool CanBeRemoved => _hasHandled && _handledTime.Add(TimeSpan.FromMilliseconds(_sleepTime)) <= DateTime.Now;
        /// <summary>
        /// 每次请求间隔时间（漏桶速率）
        /// </summary>
        private int _sleepTime;
        /// <summary>
        /// 每秒最大访问量，也是用于求漏桶的速率（漏桶仅支持QPS&lt;=1000）
        /// </summary>
        public double MaxQPS { get; }
        /// <summary>
        /// 桶最大容量，多余的请求会被丢弃。0为不限制，会按照设置排队下去
        /// </summary>
        public int LimitSize { get; }
        /// <summary>
        /// 最大乐观估计时间（即与存粹按照漏桶内的请求数目、QPS计算的时间比较，真实可调用时间一般会大于此时间）（为空则不限制）
        /// </summary>
        public TimeSpan? MaxOptimisticEstimatedTime { get; }
        /// <summary>
        /// 最大可能等待时间（超过此时间就放弃掉此次请求，该次请求被标记为完成）（为null则不限制）
        /// </summary>
        private DateTime? MaxWaitUntilTime { get; }

        /// <summary>
        /// 创建一个限流器
        /// </summary>
        /// <param name="limiterID">同一个API应该使用同一个限速器队列</param>
        /// <param name="maxQPS">每秒最大访问量，也是用于求漏桶的速率（漏桶仅支持QPS&lt;=1000）</param>
        /// <param name="limitSize">桶最大容量，多余的请求会被丢弃。0为不限制，会按照设置排队下去</param>
        /// <param name="maxWaitTimeSpan">超时时间（超过此时间就放弃掉此次请求）（为null则不限制）</param>
        /// <param name="maxOptimisticEstimatedTime">最大乐观估计时间（即与存粹按照漏桶内的请求数目、QPS计算的时间比较，真实可调用时间一般会大于此时间）（为空则不限制）</param>
        public LeakyBucketRateLimiter(string limiterID, double maxQPS, int limitSize = 0, TimeSpan? maxWaitTimeSpan = null, TimeSpan? maxOptimisticEstimatedTime = null)
        {
            _limiterID = limiterID;
            MaxQPS = maxQPS;
            LimitSize = limitSize;
            _sleepTime = (1000 / MaxQPS).Ceiling();
            if (_sleepTime < 0) _sleepTime = 1;
            MaxOptimisticEstimatedTime = maxOptimisticEstimatedTime;
            if (maxWaitTimeSpan != null) MaxWaitUntilTime = DateTime.Now.Add(maxWaitTimeSpan.Value);
            InitializeLimiterQueue();
        }
        /// <summary>
        /// 将队列中已完成的任务去除
        /// </summary>
        private void RemoveHandled(LimitedQueue<LeakyBucketRateLimiter> queue)
        {
            lock (queue.QueueLock)
            {
                if (queue.Count == 0) return;//队列为空则不处理
                var limiter = queue.Peek();
                while (limiter.CanBeRemoved)
                {
                    queue.Dequeue();
                    if (queue.Count == 0) return;
                    limiter = queue.Peek();
                }
            }
        }
        /// <summary>
        /// 初始化ID对应的限速器队列，无则添加进去
        /// </summary>
        /// <returns></returns>
        private void InitializeLimiterQueue()
        {
            if (_limiterDictionary.ContainsKey(_limiterID)) return;
            lock (_dictLock)
            {
                if (!_limiterDictionary.ContainsKey(_limiterID))
                {
                    _limiterDictionary.Add(_limiterID, new LimitedQueue<LeakyBucketRateLimiter>(LimitSize));
                }
            }
        }

        private int _startNum;
        private int _endNum = -1;
        private bool _hasInQueue;

        /// <summary>
        /// 令牌桶式请求，桶满直接返回false
        /// </summary>
        /// <returns></returns>
        public bool TokenBucketRequest()
        {
            var limiterQueue = _limiterDictionary[_limiterID];
            lock (limiterQueue.QueueLock)
            {
                RemoveHandled(limiterQueue);
                return limiterQueue.Enqueue(this);
            }
        }


        /// <summary>
        /// 开始排队请求，根据设置进行排队，超时则返回false
        /// </summary>
        /// <returns></returns>
        public bool CanRequest()
        {
            var limiterQueue = _limiterDictionary[_limiterID];
            while (true)
            {
                lock (limiterQueue.QueueLock)
                {
                    RemoveHandled(limiterQueue);
                    if (!_hasInQueue)
                    {
                        if (LimitSize != 0 && limiterQueue.Count >= LimitSize) return this.ReturnError($"请求桶已满{LimitSize}个限制");//大于桶的大小，直接拒绝请求
                        _startNum = limiterQueue.DequeueTimes;
                        _endNum = limiterQueue.Count + _startNum;
                        if (MaxOptimisticEstimatedTime != null &&
                            TimeSpan.FromMilliseconds((_endNum - _startNum) * _sleepTime) > MaxOptimisticEstimatedTime.Value) return this.ReturnError($"前面有{limiterQueue.Count}个排队，超过设定乐观预估时间{MaxOptimisticEstimatedTime}");//超过设定的预估时间
                        limiterQueue.Enqueue(this);
                        _hasInQueue = true;
                    }
                    if (limiterQueue.DequeueTimes == _endNum) return true;//说明已经轮到了
                    if (MaxWaitUntilTime != null && DateTime.Now > MaxWaitUntilTime)//当前已经超过预估时间还未成功（一般是前面队列请求失败的情况）
                    {
                        Dispose();
                        _sleepTime = 0;//快速结束，因为判断是否可移除的条件还有超过当前时间+平均速率
                        return this.ReturnError("排队超时");
                    }
                }

                var remnantCount = _endNum - limiterQueue.DequeueTimes;
                var waitTimeFactor = 1000.0 / remnantCount + 1;
                var sleepTime = remnantCount * _sleepTime / waitTimeFactor;
                if (sleepTime <= 0) sleepTime = 1;
                Thread.Sleep(sleepTime.Ceiling());
            }

        }
        /// <summary>
        /// （异步请求）开始排队请求，根据设置进行排队，超时则返回false
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CanRequestAsync() => await Task.Factory.StartNew(CanRequest);

        /// <summary>
        /// 指示该任务已经结束
        /// </summary>
        public void Dispose()
        {
            _hasHandled = true;//using结束后一般就是完成了任务
            _handledTime = DateTime.Now;
        }

        public string? ErrorMsg { get; set; }
    }
}