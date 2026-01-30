namespace Monica.StateStore.UI.Models;

/// <summary>
/// 表示 StateStore 中的 Key 信息
/// </summary>
public class StateStoreKeyInfo
{
    /// <summary>
    /// Key 名称
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 原始 JSON 值 (用于显示)
    /// </summary>
    public string? RawValue { get; set; }

    /// <summary>
    /// 值是否已加载
    /// </summary>
    public bool IsValueLoaded { get; set; }

    /// <summary>
    /// ETag (用于乐观并发控制)
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// 加载值时的错误信息
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// 创建/更新 Key 的请求模型
/// </summary>
public class StateStoreKeyUpdateRequest
{
    /// <summary>
    /// Key 名称
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// JSON 值
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// ETag (用于乐观并发控制)
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public TimeSpan? TTL { get; set; }
}

/// <summary>
/// Key 扫描结果 (支持分页)
/// </summary>
public record KeyScanResult
{
    /// <summary>
    /// 匹配的 Key 列表
    /// </summary>
    public List<string> Keys { get; init; } = [];

    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 是否还有更多
    /// </summary>
    public bool HasMore { get; init; }
}
