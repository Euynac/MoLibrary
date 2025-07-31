using System.Text.Json.Nodes;

namespace MoLibrary.Configuration.Dashboard.Model;

public class DtoUpdateConfig
{
    /// <summary>
    /// 配置项/配置类所对应AppID
    /// </summary>
    public required string AppId { get; set; }
    /// <summary>
    /// 配置项/配置类Key
    /// </summary>
    public required string Key { get; set; }
    /// <summary>
    /// 配置项/配置类修改值
    /// </summary>
    public JsonNode? Value { get; set; }
}

public class DtoUpdateConfigRes
{
    /// <summary>
    /// 相应配置类AppID
    /// </summary>
    public string? AppId { get; set; }
    /// <summary>
    /// 相应配置类Key
    /// </summary>
    public required string ConfigKey { get; set; }
    /// <summary>
    /// 配置类标题
    /// </summary>
    public required string ConfigTitle { get; set; }
    /// <summary>
    /// 最终配置类值
    /// </summary>
    public required JsonNode? NewConfigValue { get; set; }
    /// <summary>
    /// 原始配置类值
    /// </summary>
    public required JsonNode? OldConfigValue { get; set; }
   
}