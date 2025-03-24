namespace MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;

/// <summary>
/// 请求含排序规则
/// </summary>
public interface IHasRequestSorting
{
    /// <summary>
    /// Sorting information.
    /// Should include sorting field and optionally a direction (ASC or DESC)
    /// Can contain more than one field separated by comma (,).
    /// </summary>
    /// <example>
    /// Examples:
    /// "Name"
    /// "Name DESC"
    /// "Name ASC, Age DESC"
    /// </example>
    public string? Sorting { get; set; }
}