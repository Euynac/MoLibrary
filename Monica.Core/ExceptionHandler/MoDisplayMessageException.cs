using Monica.Tool.Extensions;
using Monica.Tool.MoResponse;

namespace Monica.Core.ExceptionHandler;

/// <summary>
/// 带用户友好消息的异常基类
/// 用于分离前端显示消息和开发者调试信息
/// </summary>
/// <remarks>
/// <para>DisplayMessage: 用户友好的错误消息，会设置到 Res.Message 中显示给前端用户</para>
/// <para>TechnicalDetail: 技术细节信息，会设置到 Res.ExtraInfo["detail"] 中供开发者调试</para>
/// </remarks>
public abstract class MoDisplayMessageException : Exception
{
    /// <summary>
    /// 用户友好的错误消息（显示给前端）
    /// </summary>
    public string DisplayMessage { get; }

    /// <summary>
    /// 技术细节（用于日志和调试，存入 ExtraInfo）
    /// </summary>
    public string? TechnicalDetail { get; }

    /// <summary>
    /// 响应码（默认 BadRequest，子类可重写）
    /// </summary>
    public virtual ResponseCode ResponseCode => ResponseCode.BadRequest;

    /// <summary>
    /// 创建带用户友好消息的异常
    /// </summary>
    /// <param name="displayMessage">用户友好的错误消息</param>
    /// <param name="technicalDetail">技术细节（可选）</param>
    protected MoDisplayMessageException(string displayMessage, string? technicalDetail = null)
        : base($"{displayMessage}{technicalDetail?.BeAfter(": ")}")
    {
        DisplayMessage = displayMessage;
        TechnicalDetail = technicalDetail;
    }
}
