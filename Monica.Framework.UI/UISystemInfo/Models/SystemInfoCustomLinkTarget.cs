using System.ComponentModel;

namespace Monica.Framework.UI.UISystemInfo.Models;

/// <summary>
/// Defines the supported HTML target values for a custom system information link.
/// </summary>
public enum SystemInfoCustomLinkTarget
{
    /// <summary>
    /// Opens the link in the current browsing context.
    /// </summary>
    [Description("_self")]
    CurrentTab,

    /// <summary>
    /// Opens the link in a new tab or window.
    /// </summary>
    [Description("_blank")]
    NewTab,

    /// <summary>
    /// Opens the link in the parent browsing context.
    /// </summary>
    [Description("_parent")]
    ParentFrame,

    /// <summary>
    /// Opens the link in the top-level browsing context.
    /// </summary>
    [Description("_top")]
    TopFrame
}
