using MoLibrary.Configuration.Model;
using MoLibrary.Configuration.Providers;
using System.Net;
using System.Text;
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