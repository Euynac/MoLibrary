namespace Monica.JobScheduler.Metadata;

/// <summary>
/// JobDefinition query conditions
/// </summary>
public record JobDefinitionQuery
{
    /// <summary>
    /// Filter by project name
    /// </summary>
    public string? FromProject { get; init; }

    /// <summary>
    /// Whether to include soft deleted definitions
    /// </summary>
    public bool IncludeDeleted { get; init; } = false;

    /// <summary>
    /// Page number (starting from 1)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// page size
    /// </summary>
    public int PageSize { get; init; } = 20;
}
