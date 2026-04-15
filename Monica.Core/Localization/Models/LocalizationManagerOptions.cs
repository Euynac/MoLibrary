using Monica.Core.Localization.Services;

namespace Monica.Core.Localization.Models;

/// <summary>
/// Defines the runtime options used by <see cref="LocalizationManager"/>.
/// </summary>
public sealed class LocalizationManagerOptions
{
    /// <summary>
    /// Gets or sets the default culture used as the final fallback when a key is missing from the current UI culture.
    /// </summary>
    public string DefaultCulture { get; set; } = "zh-CN";

    /// <summary>
    /// Gets or sets the supported UI cultures that should be loaded from embedded localization resources.
    /// </summary>
    public List<string> SupportedCultures { get; set; } = ["zh-CN", "en-US"];

    internal LocalizationManagerOptions Clone()
    {
        return new LocalizationManagerOptions
        {
            DefaultCulture = DefaultCulture,
            SupportedCultures = [.. SupportedCultures]
        };
    }
}
