using MoLibrary.Configuration.Model;
using MoLibrary.Configuration.Providers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    /// <summary>
    /// 获取指定配置类的修改项并按配置类组织为Dictionary结构
    /// </summary>
    public Dictionary<string, List<ModifiedConfigItem>> GetModifiedItemsByConfig()
    {
        return ModifiedItems.Values
            .GroupBy(item => item.ConfigKey)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    /// <summary>
    /// 为指定配置类生成API调用请求的JSON数据
    /// </summary>
    /// <param name="configKey">配置类Key</param>
    /// <returns>DtoUpdateConfig对象，用于API调用</returns>
    public DtoUpdateConfig BuildUpdateConfigRequest(string configKey)
    {
        if (!OriginalConfigs.TryGetValue(configKey, out var originalConfig))
            throw new ArgumentException($"未找到配置类 {configKey} 的原始数据", nameof(configKey));

        var modifiedItems = GetModifiedItemsForConfig(configKey);
        if (!modifiedItems.Any())
            throw new ArgumentException($"配置类 {configKey} 没有修改项", nameof(configKey));

        var appId = modifiedItems.First().AppId;

        // 验证所有修改项都属于同一个AppId
        if (modifiedItems.Any(item => item.AppId != appId))
        {
            throw new ArgumentException("所有修改项必须属于同一AppId");
        }

        // 构建完整的配置类JSON，包含所有配置项（修改的和未修改的）
        var configJson = new Dictionary<string, object?>();
        
        // 创建修改项的字典以便快速查找
        var modifiedDict = modifiedItems.ToDictionary(item => item.ModifiedItem.Name, item => item.ModifiedItem);

        // 遍历原始配置的所有配置项
        foreach (var originalItem in originalConfig.Items)
        {
            if (modifiedDict.TryGetValue(originalItem.Name, out var modifiedItem))
            {
                // 如果有修改，使用修改后的值
                configJson[originalItem.Name] = modifiedItem.Value;
            }
            else
            {
                // 如果没有修改，保留原值
                configJson[originalItem.Name] = originalItem.Value;
            }
        }

        return new DtoUpdateConfig
        {
            AppId = appId,
            Key = configKey,
            Value = JsonSerializer.SerializeToNode(configJson, JsonFileProviderConventions.JsonSerializerOptions)
        };
    }

    /// <summary>
    /// 为所有修改的配置类生成API调用请求列表
    /// </summary>
    /// <returns>DtoUpdateConfig对象列表，每个对应一个配置类的修改</returns>
    public List<DtoUpdateConfig> BuildAllUpdateConfigRequests()
    {
        var modifiedConfigKeys = GetModifiedConfigKeys();
        var requests = new List<DtoUpdateConfig>();

        foreach (var configKey in modifiedConfigKeys)
        {
            requests.Add(BuildUpdateConfigRequest(configKey));
        }

        return requests;
    }

    /// <summary>
    /// 获取指定配置类的API调用预览JSON字符串
    /// </summary>
    /// <param name="configKey">配置类Key</param>
    /// <param name="modifiedItems">该配置类的修改项列表（参数保留用于兼容性，实际使用内部数据）</param>
    /// <returns>格式化的JSON字符串，用于预览</returns>
    public string GetApiCallPreviewJson(string configKey, List<ModifiedConfigItem> modifiedItems)
    {
        var request = BuildUpdateConfigRequest(configKey);
        
        var preview = new
        {
            Method = "POST",
            Endpoint = "/api/configuration/update",
            Headers = new { ContentType = "application/json" },
            Body = request
        };

        return JsonSerializer.Serialize(preview, JsonFileProviderConventions.JsonSerializerOptions);
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
        var originalJson = JsonSerializer.Serialize(OriginalItem.Value, JsonFileProviderConventions.JsonSerializerOptions);
        var modifiedJson = JsonSerializer.Serialize(ModifiedItem.Value, JsonFileProviderConventions.JsonSerializerOptions);
        
        return new ConfigurationDiff
        {
            OriginalJson = originalJson,
            ModifiedJson = modifiedJson,
            OriginalJsonHighlighted = HighlightDifferences(originalJson, modifiedJson, true),
            ModifiedJsonHighlighted = HighlightDifferences(originalJson, modifiedJson, false),
            HasChanges = originalJson != modifiedJson
        };
    }
    
    /// <summary>
    /// 高亮JSON差异
    /// </summary>
    private static string HighlightDifferences(string originalJson, string modifiedJson, bool isOriginal)
    {
        if (originalJson == modifiedJson) return WebUtility.HtmlEncode(isOriginal ? originalJson : modifiedJson);
        
        try
        {
            // 解析JSON为对象进行结构化比较
            var originalDoc = JsonDocument.Parse(originalJson);
            var modifiedDoc = JsonDocument.Parse(modifiedJson);
            
            // 使用基于值的差异检测
            return HighlightJsonElementDifferences(originalDoc.RootElement, modifiedDoc.RootElement, isOriginal);
        }
        catch
        {
            // JSON解析失败时，使用行级比较作为后备方案
            return HighlightTextDifferences(originalJson, modifiedJson, isOriginal);
        }
    }
    
    /// <summary>
    /// 基于JsonElement的差异检测和高亮
    /// </summary>
    private static string HighlightJsonElementDifferences(JsonElement original, JsonElement modified, bool isOriginal)
    {
        var targetElement = isOriginal ? original : modified;
        var compareElement = isOriginal ? modified : original;
        
        return FormatJsonElementWithHighlighting(targetElement, compareElement, isOriginal, 0);
    }
    
    /// <summary>
    /// 格式化JsonElement并添加差异高亮
    /// </summary>
    private static string FormatJsonElementWithHighlighting(JsonElement target, JsonElement compare, bool isOriginal, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        var result = new StringBuilder();
        
        switch (target.ValueKind)
        {
            case JsonValueKind.Object:
                result.AppendLine("{");
                var targetProps = target.EnumerateObject().ToList();
                var compareProps = compare.ValueKind == JsonValueKind.Object 
                    ? compare.EnumerateObject().ToDictionary(p => p.Name, p => p.Value)
                    : new Dictionary<string, JsonElement>();
                
                for (int i = 0; i < targetProps.Count; i++)
                {
                    var prop = targetProps[i];
                    var hasCompareValue = compareProps.TryGetValue(prop.Name, out var compareProp);
                    var isLastProperty = i == targetProps.Count - 1;
                    
                    result.Append($"{indentStr}  \"{WebUtility.HtmlEncode(prop.Name)}\": ");
                    
                    if (!hasCompareValue || !JsonElementsEqual(prop.Value, compareProp))
                    {
                        // 属性值有差异，高亮显示
                        var cssClass = isOriginal ? "diff-removed" : "diff-added";
                        var valueStr = FormatJsonElementWithHighlighting(prop.Value, hasCompareValue ? compareProp : new JsonElement(), isOriginal, indent + 1);
                        result.Append($"<span class=\"{cssClass}\">{valueStr}</span>");
                    }
                    else
                    {
                        // 属性值相同，正常显示
                        result.Append(FormatJsonElementWithHighlighting(prop.Value, compareProp, isOriginal, indent + 1));
                    }
                    
                    if (!isLastProperty) result.Append(",");
                    result.AppendLine();
                }
                result.Append($"{indentStr}}}");
                break;
                
            case JsonValueKind.Array:
                result.AppendLine("[");
                var targetArray = target.EnumerateArray().ToList();
                var compareArray = compare.ValueKind == JsonValueKind.Array 
                    ? compare.EnumerateArray().ToList() 
                    : new List<JsonElement>();
                
                for (int i = 0; i < targetArray.Count; i++)
                {
                    var item = targetArray[i];
                    var hasCompareItem = i < compareArray.Count;
                    var compareItem = hasCompareItem ? compareArray[i] : new JsonElement();
                    var isLastItem = i == targetArray.Count - 1;
                    
                    result.Append($"{indentStr}  ");
                    
                    if (!hasCompareItem || !JsonElementsEqual(item, compareItem))
                    {
                        // 数组项有差异，高亮显示
                        var cssClass = isOriginal ? "diff-removed" : "diff-added";
                        var itemStr = FormatJsonElementWithHighlighting(item, compareItem, isOriginal, indent + 1);
                        result.Append($"<span class=\"{cssClass}\">{itemStr}</span>");
                    }
                    else
                    {
                        // 数组项相同，正常显示
                        result.Append(FormatJsonElementWithHighlighting(item, compareItem, isOriginal, indent + 1));
                    }
                    
                    if (!isLastItem) result.Append(",");
                    result.AppendLine();
                }
                result.Append($"{indentStr}]");
                break;
                
            case JsonValueKind.String:
                result.Append($"\"{WebUtility.HtmlEncode(target.GetString()!)}\"");
                break;
                
            case JsonValueKind.Number:
                result.Append(target.GetRawText());
                break;
                
            case JsonValueKind.True:
            case JsonValueKind.False:
                result.Append(target.GetBoolean().ToString().ToLower());
                break;
                
            case JsonValueKind.Null:
                result.Append("null");
                break;
                
            default:
                result.Append(WebUtility.HtmlEncode(target.GetRawText()));
                break;
        }
        
        return result.ToString();
    }
    
    /// <summary>
    /// 比较两个JsonElement是否相等
    /// </summary>
    private static bool JsonElementsEqual(JsonElement element1, JsonElement element2)
    {
        if (element1.ValueKind != element2.ValueKind) return false;
        
        switch (element1.ValueKind)
        {
            case JsonValueKind.Object:
                var props1 = element1.EnumerateObject().ToList();
                var props2 = element2.EnumerateObject().ToList();
                if (props1.Count != props2.Count) return false;
                
                var dict2 = props2.ToDictionary(p => p.Name, p => p.Value);
                return props1.All(p1 => dict2.TryGetValue(p1.Name, out var p2) && JsonElementsEqual(p1.Value, p2));
                
            case JsonValueKind.Array:
                var array1 = element1.EnumerateArray().ToList();
                var array2 = element2.EnumerateArray().ToList();
                if (array1.Count != array2.Count) return false;
                
                return array1.Zip(array2, JsonElementsEqual).All(equal => equal);
                
            default:
                return element1.GetRawText() == element2.GetRawText();
        }
    }
    
    /// <summary>
    /// 文本级差异检测（后备方案）
    /// </summary>
    private static string HighlightTextDifferences(string originalText, string modifiedText, bool isOriginal)
    {
        var targetText = isOriginal ? originalText : modifiedText;
        var lines = targetText.Split('\n');
        var result = new List<string>();
        
        foreach (var line in lines)
        {
            result.Add(WebUtility.HtmlEncode(line));
        }
        
        return string.Join("\n", result);
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
    /// 原始JSON（带高亮）
    /// </summary>
    public required string OriginalJsonHighlighted { get; set; }
    
    /// <summary>
    /// 修改后JSON（带高亮）
    /// </summary>
    public required string ModifiedJsonHighlighted { get; set; }
    
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