namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted source state snapshot.
/// </summary>
public sealed class ConfigurationSourceStateEntity
{
    public string SourceKey { get; set; } = "";

    public string Kind { get; set; } = "";

    public int Priority { get; set; }

    public bool IsWritable { get; set; }

    public DateTimeOffset? LastReloadTime { get; set; }

    public string? LastReloadResult { get; set; }

    public string? LastError { get; set; }
}
