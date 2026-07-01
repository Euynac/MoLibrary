namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted unified configuration version summary.
/// </summary>
public sealed class ConfigurationUnifiedVersionEntity
{
    public long Version { get; set; }

    public string? MutationGroupId { get; set; }

    public string TriggerDefinitionKeysJson { get; set; } = "[]";

    public string DefinitionKeysJson { get; set; } = "[]";

    public int DefinitionCount { get; set; }

    public DateTime CreatedTime { get; set; }

    public string? ModifierId { get; set; }

    public string? ModifierName { get; set; }

    public string? Reason { get; set; }
}
