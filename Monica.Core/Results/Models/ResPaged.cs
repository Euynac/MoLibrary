using System.Dynamic;
using System.Text.Json.Serialization;
using Monica.Tool.Extensions;

namespace Monica.Core.Results;

/// <summary>
/// Unified pagination response model
/// </summary>
/// <typeparam name="TDto"></typeparam>
public class ResPaged<TDto> : IResultEnvelope
{
    public string? Message { get; set; }

    public ResStatus Status { get; set; } = ResStatus.Unknown;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? Metadata { get; set; }

    public PageData Data { get; set; }

    public class PageData(int? sum, IReadOnlyList<TDto>? items)
    {
        /// <summary>
        /// Total data
        /// </summary>
        [JsonPropertyName("sum")]
        public int? Sum { get; set; } = sum;

        /// <summary>
        /// Current data list
        /// </summary>
        [JsonPropertyName("items")]
        public IReadOnlyList<TDto>? Items { get; set; } = items;

        /// <summary>
        /// Amount of data per page
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int? PageSize { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        [JsonPropertyName("currentPage")]
        public int? CurrentPage { get; set; }

        /// <summary>
        /// Total pages
        /// </summary>
        [JsonPropertyName("totalPages")]
        public int? TotalPages => ((double?)Sum / PageSize)?.Ceiling();

        /// <summary>
        /// Is it possible to page forward
        /// </summary>
        [JsonPropertyName("hasPrevious")]
        public bool? HasPrevious => CurrentPage == null ? null : CurrentPage > 1;

        /// <summary>
        /// Is it possible to turn pages backward?
        /// </summary>
        [JsonPropertyName("hasNext")]
        public bool? HasNext => CurrentPage == null ? null : CurrentPage < TotalPages;

        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }
    }

    public ResPaged()
    {
        Data = new PageData(null, null);
    }

    public ResPaged(int sum, IReadOnlyList<TDto> items)
    {
        Status = ResStatus.Ok;
        Data = new PageData(sum, items);
    }

    public ResPaged(int sum, IReadOnlyList<TDto> items, int? currentPage, int? pageSize)
    {
        Status = ResStatus.Ok;
        Data = new PageData(sum, items)
        {
            CurrentPage = currentPage,
            PageSize = pageSize
        };
    }

    public ResPaged(int sum, IReadOnlyList<TDto> items, int? currentPage, int? pageSize, string? cursor)
    {
        Status = ResStatus.Ok;
        Data = new PageData(sum, items)
        {
            CurrentPage = currentPage,
            PageSize = pageSize,
            Cursor = cursor
        };
    }

    /// <summary>
    /// Get inheritable error information
    /// </summary>
    /// <returns></returns>
    public Res ToRes() => this;

    /// <summary>
    /// Extract as new response data
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res(ResPaged<TDto> res) => new(res.Message ?? "", res.Status)
    {
        Metadata = res.Metadata
    };

    public static implicit operator ResPaged<TDto>(Res res) => new(0, [])
    {
        Message = res.Message,
        Status = res.Status,
        Metadata = res.Metadata
    };

    public static implicit operator ResPaged<TDto>(string res) => new(0, [])
    {
        Message = res,
        Status = ResStatus.BadRequest
    };
}
