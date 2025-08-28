using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Model;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.Configuration.Providers;

/// <summary>
/// 本地Json配置文件提供者
/// </summary>
/// <param name="card"></param>
public class LocalJsonFileProvider(MoConfigurationCard card)
{
    private readonly HashSet<string> _skipCheckJsonPath = [];

    /// <summary>
    /// 配置如何处理删除的属性
    /// </summary>
    public enum RemovedPropertyHandling
    {
        /// <summary>
        /// 从配置文件中移除
        /// </summary>
        Remove,
        
        /// <summary>
        /// 保留但注释掉
        /// </summary>
        Comment
    }
    
    /// <summary>
    /// 拟将配置卡片存储到文件后的内容，取配置类实际默认值
    /// </summary>
    /// <returns></returns>
    public string GetDefaultFileContents()
    {
        // 确定配置节点名称
        var sectionName = card.Key;
        // 获取当前配置类的默认实例
        var defaultObj = Activator.CreateInstance(card.Configuration.ConfigType);
        if (defaultObj == null)
        {
            MoConfigurationManager.Logger.LogError("无法为配置类创建默认实例: {ConfigType}", card.Configuration.ConfigType.FullName);
            throw new InvalidOperationException($"配置类{card.Configuration.ConfigType.FullName}无法生成默认配置值，请检查是否有无参构造函数");
        }

        // 创建默认配置的JSON节点
        var defaultJson = JsonSerializer.Serialize(
            new Dictionary<string, object> { { sectionName, defaultObj } },
            JsonFileProviderConventions.JsonSerializerOptions);
        return defaultJson;
    }

    #region 生成配置文件

    /// <summary>
    /// Generates configuration files for the specified configuration card.
    /// </summary>
    internal void GenAndRegisterConfigurationFiles()
    {
        if (!MoConfigurationManager.Setting.GenerateFileForEachOption) return;
        var filename = $"{card.FromProjectName}.{card.Configuration.Name}.json";
        if (MoConfigurationManager.Setting.GenerateOptionFileParentDirectory is { } parent && !string.IsNullOrWhiteSpace(parent))
        {
            filename = Path.Combine(parent, filename);
        }

        var path = GeneralExtensions.GetRelativePathInRunningPath(filename);
        
        if (!File.Exists(path))
        {
            var directory = FileTool.GetDirectoryPath(path)!;
            Directory.CreateDirectory(directory);
            var contents = GetDefaultFileContents();    
            File.WriteAllText(path, contents, Encoding.UTF8);
        }
        else
        {
            foreach (var item in card.Configuration.OptionItems.Where(p=>IsShouldJumpCheckType(p.PropertyInfo.PropertyType)))
            {
                _skipCheckJsonPath.Add(item.Key.Replace(":", "."));
            }

            // 检查配置类是否有变化，并更新文件
            UpdateConfigFile(path, MoConfigurationManager.Setting.RemovedPropertyHandling);
        }

        ((ConfigurationManager)MoConfigurationManager.AppConfiguration).AddJsonFile(path, false, true);
    }
    
    /// <summary>
    /// 记录删除属性的信息
    /// </summary>
    private class RemovedPropertyInfo
    {
        /// <summary>
        /// 完整属性路径
        /// </summary>
        public string Path { get; init; } = string.Empty;
        
        /// <summary>
        /// 属性值（JSON格式）
        /// </summary>
        public JsonNode? Value { get; init; }
        
        /// <summary>
        /// 删除时间
        /// </summary>
        public DateTime RemovedTime { get; init; } = DateTime.Now;
    }
    
    /// <summary>
    /// 检查并更新配置文件以匹配当前配置类结构
    /// </summary>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="removedPropertyHandling">如何处理被移除的属性</param>
    private void UpdateConfigFile(string filePath, RemovedPropertyHandling removedPropertyHandling = RemovedPropertyHandling.Comment)
    {
        try
        {
            var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
            // 读取现有的JSON文件
            var existingJson = File.ReadAllText(filePath);
            var existingJsonDocument = JsonDocument.Parse(existingJson, documentOptions);
            var jsonObject = JsonNode.Parse(existingJson, documentOptions: documentOptions)?.AsObject();
            
            if (jsonObject == null)
            {
                MoConfigurationManager.Logger.LogWarning("无法解析配置文件: {FilePath}，将创建新文件", filePath);
                File.WriteAllText(filePath, GetDefaultFileContents(), Encoding.UTF8);
                return;
            }
            // 确定配置节点名称
            var sectionName = card.Key;
            var defaultJson = GetDefaultFileContents();
            var defaultJsonNode = JsonNode.Parse(defaultJson)?.AsObject();
            
            if (defaultJsonNode == null)
            {
                MoConfigurationManager.Logger.LogWarning("无法解析默认配置: {ConfigType}", card.Configuration.ConfigType.FullName);
                return;
            }
            
            // 检查节点是否存在
            if (!jsonObject.TryGetPropertyValue(sectionName, out var sectionNode))
            {
                // 不存在则添加
                jsonObject[sectionName] = defaultJsonNode[sectionName]?.DeepClone();
                File.WriteAllText(filePath, jsonObject.ToJsonString(JsonFileProviderConventions.JsonSerializerOptions), Encoding.UTF8);
                MoConfigurationManager.Logger.LogInformation("配置文件添加了新节点: {SectionName}", sectionName);
                return;
            }
            
            // 获取默认配置的属性列表
            var defaultSection = defaultJsonNode[sectionName]?.AsObject();
            if (defaultSection == null) return;
            
            // 获取现有配置的属性列表
            var existingSection = sectionNode?.AsObject();
            if (existingSection == null) return;
            
            // 用来收集需要添加到文件末尾的所有删除属性的历史记录
            var removedProperties = new List<RemovedPropertyInfo>();
            
            // 提取现有文件中的历史记录注释（如果有）
            var existingRemovedProperties = ExtractRemovedPropertiesHistory(existingJson);
            if (existingRemovedProperties.Count > 0)
            {
                removedProperties.AddRange(existingRemovedProperties);
            }

            // 递归比较默认对象和现有对象的结构差异
            var fileChanged = RecursivelyUpdateProperties(defaultSection, existingSection, sectionName, removedProperties);
            
            // 保存更新后的文件
            if (fileChanged || removedProperties.Count > existingRemovedProperties.Count)
            {
                // 序列化更新后的JSON对象
                var updatedJson = jsonObject.ToJsonString(JsonFileProviderConventions.JsonSerializerOptions);
                
                // 处理被移除的属性注释（如果有）
                if (removedPropertyHandling == RemovedPropertyHandling.Comment && removedProperties.Count > 0)
                {
                    updatedJson = AddRemovedPropertiesAsComments(updatedJson, removedProperties);
                }
                
                File.WriteAllText(filePath, updatedJson, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            MoConfigurationManager.Logger.LogError(ex, "更新配置文件失败: {FilePath}", filePath);
        }
    }
    
    /// <summary>
    /// 递归比较和更新JSON对象的属性结构
    /// </summary>
    /// <param name="defaultObj">默认配置对象</param>
    /// <param name="existingObj">现有配置对象</param>
    /// <param name="currentPath">当前属性路径</param>
    /// <param name="removedProperties">收集被移除的属性信息</param>
    /// <returns>如果文件有变化则返回true</returns>
    private bool RecursivelyUpdateProperties(JsonObject defaultObj, JsonObject existingObj, string currentPath, List<RemovedPropertyInfo> removedProperties)
    {

        if (_skipCheckJsonPath.Contains(currentPath)) // 如果是字典加入跳过比较列表
        {
            MoConfigurationManager.Logger.LogDebug($"跳过Dictionary类型节点的内部结构比较: {currentPath}");
            return false;
        }
        var hasChanges = false;

        // 检查是否有新属性需要添加
        foreach (var property in defaultObj)
        {
            var propertyPath = $"{currentPath}.{property.Key}";

            if (!existingObj.ContainsKey(property.Key))
            {
                // 添加新属性
                existingObj[property.Key] = property.Value?.DeepClone();
                hasChanges = true;
                MoConfigurationManager.Logger.LogInformation("配置文件添加了新属性: {PropertyPath}", propertyPath);
            }
            else if (property.Value is JsonObject defaultChildObj &&
                     existingObj[property.Key] is JsonObject existingChildObj)
            {
                // 递归处理嵌套对象
                var childChanged = RecursivelyUpdateProperties(
                    defaultChildObj, 
                    existingChildObj, 
                    propertyPath,
                    removedProperties);
                
                hasChanges = hasChanges || childChanged;
            }
        }
        
        // 检查是否有需要移除的属性
        var propertiesToRemove = new List<string>();
        
        foreach (var property in existingObj)
        {
            var propertyPath = $"{currentPath}.{property.Key}";

            if (!defaultObj.ContainsKey(property.Key))
            {
                // 记录被移除的属性信息
                removedProperties.Add(new RemovedPropertyInfo
                {
                    Path = propertyPath,
                    Value = property.Value?.DeepClone(),
                    RemovedTime = DateTime.Now
                });
                
                // 如果设置为移除属性，则添加到待移除列表
                if (MoConfigurationManager.Setting.RemovedPropertyHandling.EqualsAny(RemovedPropertyHandling.Comment, RemovedPropertyHandling.Remove))
                {
                    propertiesToRemove.Add(property.Key);
                }
                
                hasChanges = true;
                MoConfigurationManager.Logger.LogInformation("发现已移除属性: {PropertyPath}", propertyPath);
            }
            // 递归检查嵌套对象，但不处理已标记为移除的属性
            else if (property.Value is JsonObject existingChildObj && 
                     defaultObj[property.Key] is JsonObject defaultChildObj &&
                     !propertiesToRemove.Contains(property.Key))
            {
                var childChanged = RecursivelyUpdateProperties(
                    defaultChildObj,
                    existingChildObj,
                    propertyPath,
                    removedProperties);
                
                hasChanges = hasChanges || childChanged;
            }
        }
        
        // 移除标记为删除的属性
        foreach (var key in propertiesToRemove)
        {
            existingObj.Remove(key);
        }
        
        return hasChanges;
    }
    
    /// <summary>
    /// 从现有JSON文件中提取已删除属性的历史记录
    /// </summary>
    /// <param name="jsonContent">JSON文件内容</param>
    /// <returns>已删除属性的历史记录</returns>
    private List<RemovedPropertyInfo> ExtractRemovedPropertiesHistory(string jsonContent)
    {
        var result = new List<RemovedPropertyInfo>();
        
        // 查找删除历史记录部分
        var historyStart = jsonContent.IndexOf("// __REMOVED_PROPERTIES_HISTORY__", StringComparison.Ordinal);
        if (historyStart == -1) return result;
        
        try
        {
            // 逐行读取历史记录
            var historySection = jsonContent[historyStart..];
            var lines = historySection.Split('\n');
            
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("//")) continue;
                
                // 提取属性路径、删除时间和属性值
                line = line[2..].Trim(); // 移除注释标记
                
                // 解析特定格式: [2023-01-01 12:00:00] path.to.property: {"value": 123}
                var timestampEnd = line.IndexOf(']');
                if (timestampEnd == -1) continue;
                
                var timestampStr = line.Substring(1, timestampEnd - 1);
                var remaining = line[(timestampEnd + 1)..].Trim();
                
                var pathEnd = remaining.IndexOf(':');
                if (pathEnd == -1) continue;
                
                var path = remaining[..pathEnd].Trim();
                var valueJson = remaining[(pathEnd + 1)..].Trim();
                
                if (DateTime.TryParse(timestampStr, out var timestamp) && 
                    !string.IsNullOrEmpty(valueJson))
                {
                    try
                    {
                        var value = JsonNode.Parse(valueJson);
                        result.Add(new RemovedPropertyInfo
                        {
                            Path = path,
                            Value = value,
                            RemovedTime = timestamp
                        });
                    }
                    catch
                    {
                        // 忽略无法解析的值
                    }
                }
            }
        }
        catch
        {
            // 如果解析失败，返回空列表
        }
        
        return result;
    }
    
    /// <summary>
    /// 将被移除的属性作为注释添加到JSON文件末尾
    /// </summary>
    /// <param name="jsonContent">序列化后的JSON内容</param>
    /// <param name="removedProperties">被移除的属性列表</param>
    /// <returns>添加了注释的JSON内容</returns>
    private string AddRemovedPropertiesAsComments(string jsonContent, List<RemovedPropertyInfo> removedProperties)
    {
        if (removedProperties.Count == 0) return jsonContent;
        
        var sb = new StringBuilder(jsonContent);
        
        // 移除现有的历史记录部分（如果有）
        var historyStart = jsonContent.IndexOf("// __REMOVED_PROPERTIES_HISTORY__", StringComparison.Ordinal);
        if (historyStart != -1)
        {
            sb.Length = historyStart;
        }
        
        // 添加空行作为分隔
        sb.AppendLine();
        sb.AppendLine("// __REMOVED_PROPERTIES_HISTORY__");
        sb.AppendLine("// 以下是被移除的配置项记录（仅供参考）:");
        
        // 按照删除时间逆序排序，保证最新删除的显示在前面
        foreach (var property in removedProperties.OrderByDescending(p => p.RemovedTime))
        {
            var valueJson = property.Value?.ToJsonString() ?? "null";
            sb.AppendLine($"// [{property.RemovedTime:yyyy-MM-dd HH:mm:ss}] {property.Path}: {valueJson}");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 此方法用于修复Dictionary属性在配置文件结构比较时的bug。
    /// 当配置类包含字典类型属性时，
    /// JSON序列化后每个Key会成为JSON对象的属性，导致RecursivelyUpdateProperties
    /// 方法将用户添加的新Key误认为是需要移除的属性。
    /// 
    /// 通过识别Dictionary类型的属性并跳过其内部键值对的结构比较，
    /// 用户可以自由地在配置文件中添加、修改、删除Dictionary的键值对，
    /// 而不会被系统误判为配置结构变更。
    /// </summary>
    private static bool IsShouldJumpCheckType(Type type)
    {
        return type.IsAssignableTo<IDictionary>();
    }
    #endregion
}