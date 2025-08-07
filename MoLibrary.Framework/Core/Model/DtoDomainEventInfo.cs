namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 领域事件信息Dto
/// </summary>
public class DtoDomainEventInfo
{
    /// <summary>
    /// 项目单元信息
    /// </summary>
    public DtoProjectUnit Info { get; set; } = null!;

    /// <summary>
    /// 事件结构信息
    /// </summary>
    public object? Structure { get; set; } = null!;
}