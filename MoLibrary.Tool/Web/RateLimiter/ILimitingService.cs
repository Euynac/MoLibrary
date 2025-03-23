using System;
using System.Threading.Tasks;

namespace MoLibrary.Tool.Web.RateLimiter
{
    /// <summary>
    /// 限流服务接口
    /// </summary>
    public interface ILimitingService : IDisposable
    {
        /// <summary>
        /// 发送请求，检测是否能够调用
        /// </summary>
        /// <returns>true：能够调用，false：不能调用</returns>
        bool Request();
        /// <summary>
        /// 带重试的异步请求（用于UI），当准许请求后返回true，失败返回false
        /// </summary>
        /// <param name="retryCount">最大重试次数，直到请求成功前，默认为10次</param>
        /// <returns></returns>
        Task<bool> RequestWithRetryAsync(int retryCount);
        /// <summary>
        /// 带重试的请求，当准许请求后返回true，失败返回false（线程会被阻塞用于不断重试）
        /// </summary>
        /// <param name="retryCount">最大重试次数，直到请求成功前，默认为10次</param>
        /// <returns></returns>
        bool RequestWithRetry(int retryCount);
    }
}
