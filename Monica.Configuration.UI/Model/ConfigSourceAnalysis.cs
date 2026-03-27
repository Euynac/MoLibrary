namespace Monica.Configuration.UI.Model;

/// <summary>
/// Configure source analysis results
/// </summary>
public class ConfigSourceAnalysis
{
    /// <summary>
    /// Configuration class name
    /// </summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>
    /// Configuration class title
    /// </summary>
    public string ConfigTitle { get; set; } = string.Empty;

    /// <summary>
    /// Are the sources of configuration items consistent?
    /// </summary>
    public bool IsConsistent { get; set; }

    /// <summary>
    /// Configuration items grouped by source
    /// </summary>
    public List<ConfigSourceGroup> SourceGroups { get; set; } = new();
}

/// <summary>
/// Configure source grouping
/// </summary>
public class ConfigSourceGroup
{
    /// <summary>
    /// Provider type
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Source information
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Configuration items under this source
    /// </summary>
    public List<ConfigItemSourceInfo> Items { get; set; } = new();
}

/// <summary>
/// Configuration item source information
/// </summary>
public class ConfigItemSourceInfo
{
    /// <summary>
    /// Configuration item Key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Configuration item title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Configuration item name
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
