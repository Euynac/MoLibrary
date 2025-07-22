namespace MoLibrary.Configuration.Dashboard.UIConfiguration.Models;

/// <summary>
/// 配置项视图模型
/// </summary>
public class ConfigurationItemViewModel
{
    /// <summary>
    /// 配置键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// 配置值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 数据类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>
    /// 描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 状态显示文本
    /// </summary>
    public string StatusText => IsActive ? "激活" : "未激活";

    /// <summary>
    /// 状态颜色
    /// </summary>
    public string StatusColor => IsActive ? "success" : "warning";
}

/// <summary>
/// 配置项详情视图模型
/// </summary>
public class ConfigurationDetailViewModel : ConfigurationItemViewModel
{
    /// <summary>
    /// 验证规则
    /// </summary>
    public List<string> ValidationRules { get; set; } = new();

    /// <summary>
    /// 配置来源
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 默认值
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// 配置分组
    /// </summary>
    public string Group { get; set; } = string.Empty;
}

/// <summary>
/// 配置历史记录视图模型
/// </summary>
public class ConfigurationHistoryViewModel
{
    /// <summary>
    /// 历史记录ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 配置键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// 旧值
    /// </summary>
    public string OldValue { get; set; } = string.Empty;

    /// <summary>
    /// 新值
    /// </summary>
    public string NewValue { get; set; } = string.Empty;

    /// <summary>
    /// 修改者
    /// </summary>
    public string ModifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime ModifiedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 操作类型
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// 版本
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 操作类型显示文本
    /// </summary>
    public string OperationText => Operation switch
    {
        "Create" => "创建",
        "Update" => "更新",
        "Delete" => "删除",
        "Rollback" => "回滚",
        _ => Operation
    };

    /// <summary>
    /// 操作类型颜色
    /// </summary>
    public string OperationColor => Operation switch
    {
        "Create" => "success",
        "Update" => "info",
        "Delete" => "error",
        "Rollback" => "warning",
        _ => "default"
    };
}

/// <summary>
/// 配置更新请求
/// </summary>
public class ConfigurationUpdateRequest
{
    /// <summary>
    /// 配置键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 新值
    /// </summary>
    public object Value { get; set; } = string.Empty;

    /// <summary>
    /// 应用ID
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// 更新说明
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// 配置回滚请求
/// </summary>
public class ConfigurationRollbackRequest
{
    /// <summary>
    /// 配置键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// 目标版本
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 回滚说明
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// 配置搜索请求
/// </summary>
public class ConfigurationSearchRequest
{
    /// <summary>
    /// 搜索关键字
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 应用ID筛选
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// 配置类型筛选
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 状态筛选
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// 排序字段
    /// </summary>
    public string SortField { get; set; } = "LastModified";

    /// <summary>
    /// 排序方向
    /// </summary>
    public bool SortDescending { get; set; } = true;

    /// <summary>
    /// 页码
    /// </summary>
    public int PageIndex { get; set; } = 0;

    /// <summary>
    /// 页大小
    /// </summary>
    public int PageSize { get; set; } = 20;
}