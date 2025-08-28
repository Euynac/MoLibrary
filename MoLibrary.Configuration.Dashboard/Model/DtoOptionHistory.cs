using System.Text.Json.Nodes;

namespace MoLibrary.Configuration.Dashboard.Model;

public class DtoOptionHistory
{
    /// <summary>
    /// 记录标题
    /// </summary>
    public required string Title { get; set; }
    /// <summary>
    /// 项目Id
    /// </summary>
    public required string AppId { get; set; }

    /// <summary>
    /// {配置类Key}:{配置项Key}
    /// </summary>
    public required string Key { get; set; }
    /// <summary>
    /// 配置值 Value
    /// </summary>
    public JsonNode? OldValue { get; set; }

    /// <summary>
    /// 配置新值
    /// </summary>
    public JsonNode? NewValue { get; set; }
    /// <summary>
    /// 配置更新时间
    /// </summary>
    public DateTime ModificationTime { get; set; }
    /// <summary>
    /// 配置更新来源人ID
    /// </summary>
    public string? ModifierId { get; set; }
    /// <summary>
    /// 配置更新来源人名
    /// </summary>
    public string? Username { get; set; }
    /// <summary>
    /// 配置版本
    /// </summary>
    public required string Version { get; set; }
}