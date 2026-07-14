namespace Monica.ProjectUnits.Models;

/// <summary>
/// Project unit alarm information
/// </summary>
public class ProjectUnitAlert
{
    /// <summary>
    /// Alarm level
    /// </summary>
    public EAlertLevel Level { get; set; }
    
    /// <summary>
    /// Alarm content
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Alarm source
    /// </summary>
    public string? Source { get; set; }
    
    /// <summary>
    /// creation time
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Alarm level enumeration
/// </summary>
public enum EAlertLevel
{
    /// <summary>
    /// information level
    /// </summary>
    Info,
    
    /// <summary>
    /// warning level
    /// </summary>
    Warning,
    
    /// <summary>
    /// error level
    /// </summary>
    Error
}