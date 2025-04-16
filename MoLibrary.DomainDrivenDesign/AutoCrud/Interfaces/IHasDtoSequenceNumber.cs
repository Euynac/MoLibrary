using System.Text.Json.Serialization;

namespace MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;

/// <summary>
/// 响应Dto包含序号信息的接口
/// </summary>
public interface IHasDtoSequenceNumber
{
    /// <summary>
    /// 当前项在列表中的序号
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Num { get; set; }
}