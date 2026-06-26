namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted configuration mutation group row.
/// </summary>
public sealed class ConfigurationMutationGroupEntity
{
    public string GroupId { get; set; } = "";

    public string Label { get; set; } = "";

    public string? Reason { get; set; }

    public string DefinitionKeysJson { get; set; } = "[]";

    public int MutationCount { get; set; }

    public DateTime CreatedTime { get; set; }

    public string? ModifierId { get; set; }

    public string? ModifierName { get; set; }

    public DateTime? RolledBackTime { get; set; }

    public string? RolledBackGroupId { get; set; }

    public string Status { get; set; } = "";
}
