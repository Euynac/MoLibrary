using MoLibrary.Configuration.Model;
using System.Text.Json;

namespace MoLibrary.Configuration.Dashboard.Model;

/// <summary>
/// 配置修改跟踪器，用于跟踪配置项的修改状态
/// </summary>
public class ConfigurationChangeTracker
{
    /// <summary>
    /// 原始配置数据
    /// </summary>
    public Dictionary<string, DtoConfig> OriginalConfigs { get; set; } = new();
    
    /// <summary>
    /// 已修改的配置项
    /// </summary>
    public Dictionary<string, ModifiedConfigItem> ModifiedItems { get; set; } = new();
    
    /// <summary>
    /// 添加或更新修改的配置项
    /// </summary>
    public void TrackChange(string configKey, DtoOptionItem originalItem, DtoOptionItem modifiedItem, string appId)
    {
        var itemKey = $"{configKey}:{originalItem.Key}";
        ModifiedItems[itemKey] = new ModifiedConfigItem
        {
            ConfigKey = configKey,
            AppId = appId,
            OriginalItem = originalItem,
            ModifiedItem = modifiedItem
        };
    }
    
    /// <summary>
    /// 撤销修改
    /// </summary>
    public void UndoChange(string configKey, string itemKey)
    {
        var key = $"{configKey}:{itemKey}";
        ModifiedItems.Remove(key);
    }
    
    /// <summary>
    /// 获取指定配置类的修改项
    /// </summary>
    public List<ModifiedConfigItem> GetModifiedItemsForConfig(string configKey)
    {
        return ModifiedItems.Values
            .Where(item => item.ConfigKey == configKey)
            .ToList();
    }
    
    /// <summary>
    /// 检查配置项是否已修改
    /// </summary>
    public bool IsItemModified(string configKey, string itemKey)
    {
        var key = $"{configKey}:{itemKey}";
        return ModifiedItems.ContainsKey(key);
    }
    
    /// <summary>
    /// 获取所有已修改的配置类
    /// </summary>
    public List<string> GetModifiedConfigKeys()
    {
        return ModifiedItems.Values
            .Select(item => item.ConfigKey)
            .Distinct()
            .ToList();
    }
    
    /// <summary>
    /// 清空所有修改记录
    /// </summary>
    public void Clear()
    {
        ModifiedItems.Clear();
    }
}

/// <summary>
/// 修改的配置项信息
/// </summary>
public class ModifiedConfigItem
{
    /// <summary>
    /// 配置类Key
    /// </summary>
    public required string ConfigKey { get; set; }
    
    /// <summary>
    /// 应用ID
    /// </summary>
    public required string AppId { get; set; }
    
    /// <summary>
    /// 原始配置项
    /// </summary>
    public required DtoOptionItem OriginalItem { get; set; }
    
    /// <summary>
    /// 修改后的配置项
    /// </summary>
    public required DtoOptionItem ModifiedItem { get; set; }
    
    /// <summary>
    /// 获取JSON差异
    /// </summary>
    public ConfigurationDiff GetDifference()
    {
        var originalJson = JsonSerializer.Serialize(OriginalItem.Value, new JsonSerializerOptions { WriteIndented = true });
        var modifiedJson = JsonSerializer.Serialize(ModifiedItem.Value, new JsonSerializerOptions { WriteIndented = true });
        
        return new ConfigurationDiff
        {
            OriginalJson = originalJson,
            ModifiedJson = modifiedJson,
            HasChanges = originalJson != modifiedJson
        };
    }
}

/// <summary>
/// 配置差异信息
/// </summary>
public class ConfigurationDiff
{
    /// <summary>
    /// 原始JSON
    /// </summary>
    public required string OriginalJson { get; set; }
    
    /// <summary>
    /// 修改后JSON
    /// </summary>
    public required string ModifiedJson { get; set; }
    
    /// <summary>
    /// 是否有变更
    /// </summary>
    public bool HasChanges { get; set; }
}

/// <summary>
/// 修改配置摘要信息
/// </summary>
public class ModifiedConfigSummary
{
    public required string ConfigKey { get; set; }
    public required string ConfigTitle { get; set; }
    public required string AppId { get; set; }
    public int ModifiedCount { get; set; }
}