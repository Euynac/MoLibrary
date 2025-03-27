using System.Text.Json.Serialization;
using MoLibrary.Core.GlobalJson.Converters;
using MoLibrary.Tool.General;

namespace MoLibrary.Configuration.Model;


public class DtoOptionItem
{
    /// <summary>
    /// 显示标题
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 配置项名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 配置项Key，用此来进行配置项修改
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 配置项描述
    /// </summary>
    public string? Desc { get; set; }
    /// <summary>
    /// 配置项值
    /// </summary>
    [JsonConverter(typeof(PreserveOriginalWithEnumStringConverter))]
    public object? Value { get; set; }

    /// <summary>
    /// 是否是离线参数，是则需要重启微服务才能生效
    /// </summary>
    public bool IsOffline { get; set; }

    /// <summary>
    /// 配置基本类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OptionItemValueBasicType Type { get; set; } = OptionItemValueBasicType.String;
    /// <summary>
    /// 配置特殊类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OptionItemValueSpecialType? SpecialType { get; set; }

    /// <summary>
    /// 正则验证表达式
    /// </summary>
    public string? RegexPattern { get; set; }

    /// <summary>
    /// 指示该配置是可空类型（可传入null）
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// 子配置结构类型
    /// </summary>
    public DtoConfig? SubStructure { get; set; }


    /// <summary>
    /// 配置Provider
    /// </summary>
    public string? Provider { get; set; }
    /// <summary>
    /// 配置来源信息
    /// </summary>
    public string? Source { get; set; }
}

public class DtoConfig
{

    /// <summary>
    /// 显示标题
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 配置类名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 配置参数类别
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 配置类描述
    /// </summary>
    public string? Desc { get; set; }

    /// <summary>
    /// 配置类相关APPID
    /// </summary>
    public string? AppId { get; set; }


    /// <summary>
    /// 配置项
    /// </summary>
    public List<DtoOptionItem> Items { get; set; }


    #region 审计字段

    /// <summary>
    /// 版本号
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 配置获取时间
    /// </summary>
    public DateTime FetchTime { get; } = DateTime.Now;
    /// <summary>
    /// 配置上一次更新时间
    /// </summary>
    public DateTime? LastModificationTime { get; set; }
    /// <summary>
    /// 配置上一次更新来源人ID
    /// </summary>
    public string? LastModifierId { get; set; }
    /// <summary>
    /// 配置上一次更新来源人名
    /// </summary>
    public string? Username { get; set; }

    #endregion

    /// <summary>
    /// 该配置类是否包含相应配置项
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool ContainsKey(string key)
    {
        return Items.Any(p => p.Key.Equals(key));
    }

    /// <summary>
    /// 获取配置类当前值
    /// </summary>
    /// <returns></returns>
    public string ToJsonValue()
    {
        return Items.Select(p => p.Value).ToJsonString() ?? "";
    }
}

public class DtoServiceConfigs
{
    /// <summary>
    /// 显示标题
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 微服务名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// AppID
    /// </summary>
    public string AppId { get; set; }

    /// <summary>
    /// 微服务内配置类
    /// </summary>
    public List<DtoConfig> Children { get; set; } = [];
}

public class DtoDomainConfigs
{
    /// <summary>
    /// 显示标题
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 子域名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 子域内微服务信息
    /// </summary>
    public List<DtoServiceConfigs> Children { get; set; } = [];
}