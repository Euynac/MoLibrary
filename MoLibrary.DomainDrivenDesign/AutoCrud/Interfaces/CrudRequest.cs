using MoLibrary.AutoModel.Interfaces;

namespace MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;

/// <summary>
/// Crud自动接口功能禁用占位Dto，指示包含该参数的功能无需生成接口
/// </summary>
public class OurCrudDisableDto
{

}

/// <summary>
/// 批量删除请求Dto
/// </summary>
public class OurCrudBulkDeleteRequestDto<TKey> : IHasRequestIds<TKey>
{
    public List<TKey> Ids { get; set; }
}


/// <summary>
/// 分页请求Dto
/// </summary>
public class OurCrudPageRequestDto : LimitedResultRequestDto, IHasRequestFilter, IHasRequestSelect, IHasRequestPage, IHasRequestFeature, IHasRequestSorting
{
    /// <inheritdoc />
    public int? Page { get; set; }
    /// <inheritdoc />
    public bool? DisablePage { get; set; }
    /// <inheritdoc />
    public string? Filter { get; set; }
    /// <inheritdoc />
    public string? Fuzzy { get; set; }
    /// <inheritdoc />
    public string? FuzzyColumns { get; set; }
    /// <inheritdoc />
    public string? SelectColumns { get; set; }
    /// <inheritdoc />
    public string? SelectExceptColumns { get; set; }
    /// <inheritdoc />
    public string? Features { get; set; }

    /// <inheritdoc />
    public int? SkipCount { get; set; }

    /// <inheritdoc />
    public string? Sorting { get; set; }

    static OurCrudPageRequestDto()
    {
        MaxMaxResultCount = 100000;
    }
}