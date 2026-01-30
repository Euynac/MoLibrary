namespace Monica.Core.ExceptionHandler;

/// <summary>
/// 异常包装器，用于为异常附加额外的上下文信息
/// 在异常处理时，会自动展开此包装器，将额外信息添加到响应的 ExtraInfo 中，并处理内部的实际异常
/// </summary>
public class MoWrapperException : Exception
{
    /// <summary>
    /// 额外信息字典，用于存储上下文信息
    /// </summary>
    public IReadOnlyDictionary<string, object?> ExtraInfo => _extraInfo;
    private readonly Dictionary<string, object?> _extraInfo = new();

    /// <summary>
    /// 创建异常包装器
    /// </summary>
    /// <param name="message">包装异常的描述信息</param>
    /// <param name="innerException">被包装的实际异常</param>
    public MoWrapperException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// 创建异常包装器（使用内部异常的消息）
    /// </summary>
    /// <param name="innerException">被包装的实际异常</param>
    /// <param name="message">可选的包装异常描述信息，如果为空则使用内部异常的消息</param>
    public MoWrapperException(Exception innerException, string? message = null)
        : base(message ?? innerException.Message, innerException)
    {
    }

    /// <summary>
    /// 添加额外信息（流畅API）
    /// </summary>
    /// <param name="key">信息键</param>
    /// <param name="value">信息值</param>
    /// <returns>当前实例，支持链式调用</returns>
    public MoWrapperException WithExtraInfo(string key, object? value)
    {
        _extraInfo[key] = value;
        return this;
    }

    /// <summary>
    /// 批量添加额外信息
    /// </summary>
    /// <param name="extraInfo">要添加的额外信息字典</param>
    /// <returns>当前实例，支持链式调用</returns>
    public MoWrapperException WithExtraInfo(IDictionary<string, object?> extraInfo)
    {
        foreach (var kvp in extraInfo)
        {
            _extraInfo[kvp.Key] = kvp.Value;
        }
        return this;
    }
}
