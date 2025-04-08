using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json.Serialization;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.MoResponse;

/// <summary>
/// 统一分页响应模型
/// </summary>
/// <typeparam name="TDto"></typeparam>
public class ResPaged<TDto> : IServiceResponse
{
    public string? Message { get; set; }
    public ResponseCode? Code { get; set; } = ResponseCode.Ok;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? ExtraInfo { get; set; }


    public PageData Data { get; set; }

    public class PageData(int? sum, IReadOnlyList<TDto>? items)
    {
        /// <summary>
        /// 数据总数
        /// </summary>
        public int? Sum { get; set; } = sum;
        /// <summary>
        /// 当前数据列表
        /// </summary>
        public IReadOnlyList<TDto>? Items { get; set; } = items;
        /// <summary>
        /// 每页数据数量
        /// </summary>
        public int? PageSize { get; set; }
        /// <summary>
        /// 当前页数
        /// </summary>
        public int? CurrentPage { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int? TotalPages => ((double?)Sum / PageSize)?.Ceiling();

        /// <summary>
        /// 是否可以向前翻页
        /// </summary>
        public bool? HasPrevious => CurrentPage == null ? null : CurrentPage > 1;
        /// <summary>
        /// 是否可以向后翻页
        /// </summary>
        public bool? HasNext => CurrentPage == null ? null : CurrentPage < TotalPages;

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
    /// <summary>
    /// 获取可继承的错误信息
    /// </summary>
    /// <returns></returns>
    public Res Inherit() => this;


    /// <summary>
    /// 提取为新响应数据
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res(ResPaged<TDto> res) => new(res.Message ?? "", res.Code ?? ResponseCode.BadRequest)
    {
        ExtraInfo = res.ExtraInfo
    };

    public static implicit operator ResPaged<TDto>(Res res) => new(0, [])
    {
        Message = res.Message,
        Code = res.Code,
        ExtraInfo = res.ExtraInfo
    };

    public static implicit operator ResPaged<TDto>(string res) => new(0, [])
    {
        Message = res, Code = ResponseCode.BadRequest
    };
}