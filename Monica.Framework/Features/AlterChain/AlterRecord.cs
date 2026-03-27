namespace Monica.Framework.Features.AlterChain;

public record AlterRecord
{
    /// <summary>
    /// Change record name
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Old value (from Entity)
    /// </summary>
    public object? OldValue { get; set; }

    /// <summary>
    /// New value (from AlterItem)
    /// </summary>
    public object? NewValue { get; set; }
    /// <summary>
    /// Whether to roll back
    /// </summary>
    public bool? IsRollback { get; set; }

    /// <summary>
    /// Target rollback ID
    /// </summary>
    public List<string>? TargetRollbackIds { get; set; }
}