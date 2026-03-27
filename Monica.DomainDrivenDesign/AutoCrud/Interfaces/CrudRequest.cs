using Monica.AutoModel.Interfaces;

namespace Monica.DomainDrivenDesign.AutoCrud.Interfaces;

/// <summary>
/// Placeholder DTO used to disable generation of a specific auto-CRUD endpoint when this parameter type is present.
/// </summary>
public class MoCrudDisableDto
{

}

/// <summary>
/// Request DTO for bulk deletion.
/// </summary>
public class MoCrudBulkDeleteRequestDto<TKey> : IHasRequestIds<TKey>
{
    public List<TKey> Ids { get; set; } = [];
}


/// <summary>
/// Default paged CRUD request DTO.
/// </summary>
public class MoCrudPageRequestDto : LimitedResultRequestDto, IHasRequestFilter, IHasRequestSelect, IHasRequestPage, IHasRequestFeature, IHasRequestSorting, IHasRequestKeysetPage
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

    public string? Cursor { get; set; }

    static MoCrudPageRequestDto()
    {
        MaxMaxResultCount = 100000;
    }
}
