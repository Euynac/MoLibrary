using System;
using System.Threading;
using System.Threading.Tasks;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.Web.RateLimiter
{
    /// <summary>
    /// 令牌桶算法限流服务。优点：控制调用的平均速率，可以处理突发高数量请求
    /// </summary>
    public class TokenBucketLimitingService : ILimitingService
    {
        /// <summary>
        /// 每秒最大访问量/请求量
        /// </summary>
        public double MaxQPS { get; private set; }
        /// <summary>
        /// 最大并发限制数量，多余请求会被丢弃。若是平台API，这里就是能同时处理的最大数量，默认是QPS。若是并发服务则是最大并发数量
        /// </summary>
        public int LimitSize { get; private set; }
        /// <summary>
        /// 固定容量的桶，里面装令牌
        /// </summary>
        private readonly LimitedQueue<object> limitedQueue = null;
        /// <summary>
        /// 用于指示添加令牌的线程什么时候停止
        /// </summary>
        private readonly CancellationTokenSource cancellationToken;
        private Task task = null;
        private readonly object lockObject = new object();//用于加锁
        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="maxQPS">最大QPS</param>
        /// <param name="limitSize">最大限制同时并发数</param>
        public TokenBucketLimitingService(double maxQPS, int limitSize)
        {
            MaxQPS = maxQPS;
            LimitSize = limitSize;

            limitedQueue = new LimitedQueue<object>(limitSize);
            for (var i = 0; i < limitSize; i++)//先放满令牌
            {
                limitedQueue.Enqueue(new object());
            }
            cancellationToken = new CancellationTokenSource();
            task = Task.Factory.StartNew(new Action(TokenProcess), cancellationToken.Token);
        }

        /// <summary>
        /// 定时添加令牌
        /// </summary>
        private void TokenProcess()
        {
            var sleep = (1000 / MaxQPS).Ceiling();
            if (sleep == 0) sleep = 1; //测试过只要不是while(true)且不sleep，就算sleep(1)CPU也没什么开销
            var start = DateTime.Now;
            while (cancellationToken.Token.IsCancellationRequested == false)
            {
                lock (lockObject)
                {
                    var isSuccess = limitedQueue.Enqueue(new object());
                }
                if (DateTime.Now - start < TimeSpan.FromMilliseconds(sleep)) //如果因为等待lock而大于需要的sleep了那就不需要sleep可以直接开始放令牌
                {
                    var newSleep = sleep - (int)(DateTime.Now - start).TotalMilliseconds;
                    if (newSleep >= 0) Thread.Sleep(newSleep);//做损失时间补偿（等待造成的时间丢失补偿回来）例如sleep是500ms，等待花费了200ms，那么只需要sleep300ms就够了
                }
                start = DateTime.Now;
            }
        }

        public void Dispose()
        {
            cancellationToken.Cancel();
        }

        /// <summary>
        /// 请求令牌
        /// </summary>
        /// <returns></returns>
        public bool Request()
        {
            return RequestWithRetry(1);
        }
        public async Task<bool> RequestWithRetryAsync(int retryCount = 10)
        {
            return await Task.Factory.StartNew(() => RequestWithRetry(retryCount));
        }

        public bool RequestWithRetry(int retryCount)
        {
            if (retryCount < 0) return false;
            while (retryCount != 0)
            {
                if (limitedQueue.Count > 0)
                {
                    lock (lockObject)
                    {
                        if (limitedQueue.Count > 0) // 若是加锁一瞬间没令牌了也不行
                        {
                            var token = limitedQueue.Dequeue();
                            if (token != null) return true;
                        }
                    }
                }

                Thread.Sleep(limitedQueue.Count == 1
                    ? (1000 / MaxQPS - 1).Ceiling()
                    : Math.Max(0, ((limitedQueue.Count - 1) * 1000 / MaxQPS).Ceiling()));
                retryCount--;
            }
            return false;
        }
    }
}
