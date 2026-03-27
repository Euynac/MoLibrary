using System.Dynamic;
using System.Text.Json.Serialization;
using Monica.Tool.Extensions;

namespace Monica.Tool.Results;

/// <summary>
/// 统一分页响应模型
/// </summary>
/// <typeparam name="TDto"></typeparam>
public class ResPaged<TDto> : IResultEnvelope
{
    [JsonPropertyName(ResJsonFieldNames.Message)]
    public string? Message { get; set; }

    [JsonPropertyName(ResJsonFieldNames.Status)]
    public ResStatus? Status { get; set; } = ResStatus.Ok;

    [JsonPropertyName(ResJsonFieldNames.Metadata)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? Metadata { get; set; }

    [JsonPropertyName(ResJsonFieldNames.Data)]
    public PageData Data { get; set; }

    public class PageData(int? sum, IReadOnlyList<TDto>? items)
    {
        /// <summary>
        /// 数据总数
        /// </summary>
        [JsonPropertyName("sum")]
        public int? Sum { get; set; } = sum;

        /// <summary>
        /// 当前数据列表
        /// </summary>
        [JsonPropertyName("items")]
        public IReadOnlyList<TDto>? Items { get; set; } = items;

        /// <summary>
        /// 每页数据数量
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int? PageSize { get; set; }

        /// <summary>
        /// 当前页数
        /// </summary>
        [JsonPropertyName("currentPage")]
        public int? CurrentPage { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        [JsonPropertyName("totalPages")]
        public int? TotalPages => ((double?)Sum / PageSize)?.Ceiling();

        /// <summary>
        /// 是否可以向前翻页
        /// </summary>
        [JsonPropertyName("hasPrevious")]
        public bool? HasPrevious => CurrentPage == null ? null : CurrentPage > 1;

        /// <summary>
        /// 是否可以向后翻页
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
        Data = new PageData(sum, items);
    }

    public ResPaged(int sum, IReadOnlyList<TDto> items, int? currentPage, int? pageSize)
    {
        Data = new PageData(sum, items)
        {
            CurrentPage = currentPage,
            PageSize = pageSize
        };
    }

    public ResPaged(int sum, IReadOnlyList<TDto> items, int? currentPage, int? pageSize, string? cursor)
    {
        Data = new PageData(sum, items)
        {
            CurrentPage = currentPage,
            PageSize = pageSize,
            Cursor = cursor
        };
    }

    /// <summary>
    /// 获取可继承的错误信息
    /// </summary>
    /// <returns></returns>
    public Res Inherit() => this;

    /// <summary>
    /// 提取为新响应数据
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res(ResPaged<TDto> res) => new(res.Message ?? "", res.Status ?? ResStatus.BadRequest)
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
