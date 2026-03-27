namespace Monica.AutoModel.Configurations;

/// <summary>
/// Fuzzy-search settings for a field.
/// </summary>
public class AutoModelFuzzSetting
{
    /// <summary>
    /// Indicates that fuzzy search is currently unsupported for this field type.
    /// </summary>
    public bool IsNotSupported { get; set; }
    /// <summary>
    /// Whether this field should be ignored during full-field fuzzy searches.
    /// </summary>
    public bool IsIgnored { get; set; }
}
