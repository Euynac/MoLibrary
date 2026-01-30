using Monica.Core.ExceptionHandler;

namespace Monica.AutoModel.Exceptions;

/// <summary>
/// AutoModel 调用执行错误基类
/// </summary>
public class AutoModelBaseException : MoDisplayMessageException
{
    /// <summary>
    /// 创建 AutoModel 异常
    /// </summary>
    /// <param name="displayMessage">用户友好的错误消息（显示给前端）</param>
    /// <param name="technicalDetail">技术细节（可选，用于开发者调试）</param>
    public AutoModelBaseException(string displayMessage, string? technicalDetail = null)
        : base(displayMessage, technicalDetail)
    {
    }
}