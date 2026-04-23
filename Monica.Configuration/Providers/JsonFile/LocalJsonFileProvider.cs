using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Models.Internal;
using Monica.Configuration.Services.Support;
using Monica.Tool.Extensions;
using Monica.Tool.IO;
using Monica.Tool.Runtime;

namespace Monica.Configuration.Providers.JsonFile;

/// <summary>
/// Local JSON configuration file provider.
/// </summary>
/// <param name="card"></param>
public class LocalJsonFileProvider(ConfigurationRegistration card)
{
    private readonly HashSet<string> _skipCheckJsonPath = [];

    /// <summary>
    /// Defines how removed properties are handled.
    /// </summary>
    public enum RemovedPropertyHandling
    {
        /// <summary>
        /// Remove them from the configuration file.
        /// </summary>
        Remove,
        
        /// <summary>
        /// Keep them as commented history records.
        /// </summary>
        Comment
    }
    
    /// <summary>
    /// Builds default file content for the current configuration card
    /// using the configuration type's default instance.
    /// </summary>
    /// <returns></returns>
    public string GetDefaultFileContents()
    {
        // Determine the configuration section name.
        var sectionName = card.Key;
        // Create the default instance of the configuration type.
        var defaultObj = Activator.CreateInstance(card.Configuration.ConfigType);
        if (defaultObj == null)
        {
            ConfigurationRuntime.Logger.LogError("无法为配置类创建默认实例: {ConfigType}", card.Configuration.ConfigType.FullName);
            throw new InvalidOperationException($"配置类{card.Configuration.ConfigType.FullName}无法生成默认配置值，请检查是否有无参构造函数");
        }

        // Create the JSON node for default configuration content.
        var defaultJson = JsonSerializer.Serialize(
            new Dictionary<string, object> { { sectionName, defaultObj } },
            JsonFileConventions.JsonSerializerOptions);
        return defaultJson;
    }

    #region 生成配置文件

    /// <summary>
    /// Generates configuration files for the specified configuration card.
    /// </summary>
    internal void GenAndRegisterConfigurationFiles()
    {
        if (!ConfigurationRuntime.Setting.GenerateFileForEachOption) return;
        var filename = card.Configuration.DefaultSourceFileName;
        if (ConfigurationRuntime.Setting.GenerateOptionFileParentDirectory is { } parent && !string.IsNullOrWhiteSpace(parent))
        {
            filename = Path.Combine(parent, filename);
        }

        var path = RuntimePathHelper.GetRelativePathInRunningPath(filename);
        
        if (!File.Exists(path))
        {
            var directory = FileSystem.GetDirectoryPath(path)!;
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

            // Check whether the configuration structure changed and update the file.
            UpdateConfigFile(path, ConfigurationRuntime.Setting.RemovedPropertyHandling);
        }

        ConfigurationRuntime.AppConfiguration.AddJsonFile(path, false, true);
    }
    
    /// <summary>
    /// Metadata for a removed property record.
    /// </summary>
    private class RemovedPropertyInfo
    {
        /// <summary>
        /// Full property path.
        /// </summary>
        public string Path { get; init; } = string.Empty;
        
        /// <summary>
        /// Property value (JSON format).
        /// </summary>
        public JsonNode? Value { get; init; }
        
        /// <summary>
        /// Removal time.
        /// </summary>
        public DateTime RemovedTime { get; init; } = DateTime.Now;
    }
    
    /// <summary>
    /// Checks and updates the configuration file to match the current configuration structure.
    /// </summary>
    /// <param name="filePath">Configuration file path.</param>
    /// <param name="removedPropertyHandling">How removed properties are handled.</param>
    private void UpdateConfigFile(string filePath, RemovedPropertyHandling removedPropertyHandling = RemovedPropertyHandling.Comment)
    {
        try
        {
            var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
            // Read the existing JSON file.
            var existingJson = File.ReadAllText(filePath);
            var existingJsonDocument = JsonDocument.Parse(existingJson, documentOptions);
            var jsonObject = JsonNode.Parse(existingJson, documentOptions: documentOptions)?.AsObject();
            
            if (jsonObject == null)
            {
                ConfigurationRuntime.Logger.LogWarning("无法解析配置文件: {FilePath}，将创建新文件", filePath);
                File.WriteAllText(filePath, GetDefaultFileContents(), Encoding.UTF8);
                return;
            }
            // Determine the configuration section name.
            var sectionName = card.Key;
            var defaultJson = GetDefaultFileContents();
            var defaultJsonNode = JsonNode.Parse(defaultJson)?.AsObject();
            
            if (defaultJsonNode == null)
            {
                ConfigurationRuntime.Logger.LogWarning("无法解析默认配置: {ConfigType}", card.Configuration.ConfigType.FullName);
                return;
            }
            
            // Check whether the target section exists.
            if (!jsonObject.TryGetPropertyValue(sectionName, out var sectionNode))
            {
                // Add the section when missing.
                jsonObject[sectionName] = defaultJsonNode[sectionName]?.DeepClone();
                File.WriteAllText(filePath, jsonObject.ToJsonString(JsonFileConventions.JsonSerializerOptions), Encoding.UTF8);
                ConfigurationRuntime.Logger.LogInformation("配置文件添加了新节点: {SectionName}", sectionName);
                return;
            }
            
            // Get default properties.
            var defaultSection = defaultJsonNode[sectionName]?.AsObject();
            if (defaultSection == null) return;
            
            // Get existing properties.
            var existingSection = sectionNode?.AsObject();
            if (existingSection == null) return;
            
            // Collect removed-property history records that will be appended as comments.
            var removedProperties = new List<RemovedPropertyInfo>();
            
            // Extract existing history comments from the current file (if any).
            var existingRemovedProperties = ExtractRemovedPropertiesHistory(existingJson);
            if (existingRemovedProperties.Count > 0)
            {
                removedProperties.AddRange(existingRemovedProperties);
            }

            // Recursively compare structure differences between default and existing objects.
            var fileChanged = RecursivelyUpdateProperties(defaultSection, existingSection, sectionName, removedProperties);
            
            // Save the updated file.
            if (fileChanged || removedProperties.Count > existingRemovedProperties.Count)
            {
                // Serialize the updated JSON object.
                var updatedJson = jsonObject.ToJsonString(JsonFileConventions.JsonSerializerOptions);
                
                // Append removed-property history comments when needed.
                if (removedPropertyHandling == RemovedPropertyHandling.Comment && removedProperties.Count > 0)
                {
                    updatedJson = AddRemovedPropertiesAsComments(updatedJson, removedProperties);
                }
                
                File.WriteAllText(filePath, updatedJson, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            ConfigurationRuntime.Logger.LogError(ex, "更新配置文件失败: {FilePath}", filePath);
        }
    }
    
    /// <summary>
    /// Recursively compares and updates JSON object property structure.
    /// </summary>
    /// <param name="defaultObj">Default configuration object.</param>
    /// <param name="existingObj">Existing configuration object.</param>
    /// <param name="currentPath">Current property path.</param>
    /// <param name="removedProperties">Collector for removed-property records.</param>
    /// <returns>Returns true when the file content changes.</returns>
    private bool RecursivelyUpdateProperties(JsonObject defaultObj, JsonObject existingObj, string currentPath, List<RemovedPropertyInfo> removedProperties)
    {

        if (_skipCheckJsonPath.Contains(currentPath)) // Dictionary nodes are skipped during structure comparison.
        {
            ConfigurationRuntime.Logger.LogDebug($"跳过Dictionary类型节点的内部结构比较: {currentPath}");
            return false;
        }
        var hasChanges = false;

        // Check whether new properties need to be added.
        foreach (var property in defaultObj)
        {
            var propertyPath = $"{currentPath}.{property.Key}";

            if (!existingObj.ContainsKey(property.Key))
            {
                // Add new property.
                existingObj[property.Key] = property.Value?.DeepClone();
                hasChanges = true;
                ConfigurationRuntime.Logger.LogInformation("配置文件添加了新属性: {PropertyPath}", propertyPath);
            }
            else if (property.Value is JsonObject defaultChildObj &&
                     existingObj[property.Key] is JsonObject existingChildObj)
            {
                // Recursively process nested objects.
                var childChanged = RecursivelyUpdateProperties(
                    defaultChildObj, 
                    existingChildObj, 
                    propertyPath,
                    removedProperties);
                
                hasChanges = hasChanges || childChanged;
            }
        }
        
        // Check whether there are properties to remove.
        var propertiesToRemove = new List<string>();
        
        foreach (var property in existingObj)
        {
            var propertyPath = $"{currentPath}.{property.Key}";

            if (!defaultObj.ContainsKey(property.Key))
            {
                // Record removed-property metadata.
                removedProperties.Add(new RemovedPropertyInfo
                {
                    Path = propertyPath,
                    Value = property.Value?.DeepClone(),
                    RemovedTime = DateTime.Now
                });
                
                // Add to removal list when removal is enabled.
                if (ConfigurationRuntime.Setting.RemovedPropertyHandling.EqualsAny(RemovedPropertyHandling.Comment, RemovedPropertyHandling.Remove))
                {
                    propertiesToRemove.Add(property.Key);
                }
                
                hasChanges = true;
                ConfigurationRuntime.Logger.LogInformation("发现已移除属性: {PropertyPath}", propertyPath);
            }
            // Recursively process nested objects, excluding properties already marked for removal.
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
        
        // Remove properties marked for deletion.
        foreach (var key in propertiesToRemove)
        {
            existingObj.Remove(key);
        }
        
        return hasChanges;
    }
    
    /// <summary>
    /// Extracts removed-property history records from an existing JSON file.
    /// </summary>
    /// <param name="jsonContent">JSON file content.</param>
    /// <returns>Removed-property history records.</returns>
    private List<RemovedPropertyInfo> ExtractRemovedPropertiesHistory(string jsonContent)
    {
        var result = new List<RemovedPropertyInfo>();
        
        // Locate the removed-history section.
        var historyStart = jsonContent.IndexOf("// __REMOVED_PROPERTIES_HISTORY__", StringComparison.Ordinal);
        if (historyStart == -1) return result;
        
        try
        {
            // Read history lines one by one.
            var historySection = jsonContent[historyStart..];
            var lines = historySection.Split('\n');
            
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("//")) continue;
                
                // Extract property path, removal time, and value.
                line = line[2..].Trim(); // Remove comment prefix.
                
                // Parse format: [2023-01-01 12:00:00] path.to.property: {"value": 123}
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
                        // Ignore values that cannot be parsed.
                    }
                }
            }
        }
        catch
        {
            // Return an empty list when parsing fails.
        }
        
        return result;
    }
    
    /// <summary>
    /// Appends removed properties as comments at the end of the JSON file.
    /// </summary>
    /// <param name="jsonContent">Serialized JSON content.</param>
    /// <param name="removedProperties">Removed-property list.</param>
    /// <returns>JSON content with appended comments.</returns>
    private string AddRemovedPropertiesAsComments(string jsonContent, List<RemovedPropertyInfo> removedProperties)
    {
        if (removedProperties.Count == 0) return jsonContent;
        
        var sb = new StringBuilder(jsonContent);
        
        // Remove existing history section (if present).
        var historyStart = jsonContent.IndexOf("// __REMOVED_PROPERTIES_HISTORY__", StringComparison.Ordinal);
        if (historyStart != -1)
        {
            sb.Length = historyStart;
        }
        
        // Add a blank line as separator.
        sb.AppendLine();
        sb.AppendLine("// __REMOVED_PROPERTIES_HISTORY__");
        sb.AppendLine("// Removed configuration item history (for reference only):");
        
        // Sort by removal time descending so the latest records appear first.
        foreach (var property in removedProperties.OrderByDescending(p => p.RemovedTime))
        {
            var valueJson = property.Value?.ToJsonString() ?? "null";
            sb.AppendLine($"// [{property.RemovedTime:yyyy-MM-dd HH:mm:ss}] {property.Path}: {valueJson}");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Fixes dictionary-structure comparison behavior.
    /// When a configuration type contains dictionary properties, each dictionary key
    /// becomes a JSON property after serialization. Without this guard, newly added
    /// user keys may be misidentified as removed properties by <c>RecursivelyUpdateProperties</c>.
    ///
    /// By recognizing dictionary-typed properties and skipping internal key/value
    /// structure comparison, users can add, modify, or remove dictionary entries
    /// freely without being treated as configuration schema changes.
    /// </summary>
    private static bool IsShouldJumpCheckType(Type type)
    {
        return type.IsAssignableTo<IDictionary>();
    }
    #endregion
}
