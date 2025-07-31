using System.ComponentModel.DataAnnotations;

namespace MoLibrary.Configuration.Model;

/// <summary>
/// 配置提供者信息
/// </summary>
public class DtoConfigurationProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 提供者类型
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 配置键值对
    /// </summary>
    public Dictionary<string, string?> ConfigurationData { get; set; } = new();
}

/// <summary>
/// 配置提供者分组信息
/// </summary>
public class DtoConfigurationProviderGroup
{
    /// <summary>
    /// 提供者组名（按类型分组）
    /// </summary>
    [Required]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// 该组中的提供者列表
    /// </summary>
    public List<DtoConfigurationProvider> Providers { get; set; } = new();
}