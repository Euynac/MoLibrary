using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json.Serialization;

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
        public int? Sum { get; set; } = sum;
        public IReadOnlyList<TDto>? Items { get; set; } = items;
    }
    public ResPaged()
    {
        Data = new PageData(null, null);
    }
    public ResPaged(int sum, IReadOnlyList<TDto> items)
    {
        Data = new PageData(sum, items);
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