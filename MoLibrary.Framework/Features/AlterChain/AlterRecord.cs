namespace MoLibrary.Framework.Features.AlterChain;

public record AlterRecord
{
    /// <summary>
    /// 变更记录名
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 旧值（来自于Entity）
    /// </summary>
    public object? OldValue { get; set; }

    /// <summary>
    /// 新值（来自于AlterItem）
    /// </summary>
    public object? NewValue { get; set; }
    /// <summary>
    /// 是否回滚
    /// </summary>
    public bool? IsRollback { get; set; }

    /// <summary>
    /// 目标回滚ID
    /// </summary>
    public List<string>? TargetRollbackIds { get; set; }
}