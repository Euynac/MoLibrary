namespace Monica.Framework.UI.UILogging.Models;

/// <summary>
/// Filter requests passed by the front end
/// </summary>
public sealed class LogFilterRequest
{
    public string? Keyword { get; set; }

    public bool OnlyCapture { get; set; }
}
