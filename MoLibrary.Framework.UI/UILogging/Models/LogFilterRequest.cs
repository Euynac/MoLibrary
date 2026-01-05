namespace MoLibrary.Framework.UI.UILogging.Models;

/// <summary>
/// 前端传递的筛选请求
/// </summary>
public sealed class LogFilterRequest
{
    public string? Keyword { get; set; }

    public bool OnlyCapture { get; set; }
}
