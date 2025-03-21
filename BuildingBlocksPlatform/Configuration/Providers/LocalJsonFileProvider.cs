using BuildingBlocksPlatform.Configuration.Model;
using Koubot.Tool.General;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BuildingBlocksPlatform.Configuration.Providers;

/// <summary>
/// 本地Json配置文件提供者
/// </summary>
/// <param name="card"></param>
public class LocalJsonFileProvider(MoConfigurationCard card)
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } =
        new() { WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip};
    
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
        var obj = Activator.CreateInstance(card.Configuration.ConfigType);
        if (obj == null)
            throw new InvalidOperationException($"配置类{card.Configuration.ConfigType.FullName}无法生成默认配置值，请检查是否有无参构造函数");
        //目前仅支持json格式
        var jsonFile = new Dictionary<string, object> { { card.SectionName ?? card.Configuration.Name, obj } };
        return JsonSerializer.Serialize(jsonFile, JsonSerializerOptions);
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
            // 检查配置类是否有变化，并更新文件
            UpdateConfigFile(path, MoConfigurationManager.Setting.RemovedPropertyHandling);
        }

        ((ConfigurationManager)MoConfigurationManager.AppConfiguration).AddJsonFile(path, false, true);
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
            
            // 获取当前配置类的默认实例
            var defaultObj = Activator.CreateInstance(card.Configuration.ConfigType);
            if (defaultObj == null)
            {
                MoConfigurationManager.Logger.LogError("无法为配置类创建默认实例: {ConfigType}", card.Configuration.ConfigType.FullName);
                return;
            }

            // 确定配置节点名称
            var sectionName = card.Key;

            // 创建默认配置的JSON节点
            var defaultJson = JsonSerializer.Serialize(
                new Dictionary<string, object> { { sectionName, defaultObj } }, 
                JsonSerializerOptions);

            var defaultJsonDocument = JsonDocument.Parse(defaultJson);
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
                File.WriteAllText(filePath, jsonObject.ToJsonString(JsonSerializerOptions), Encoding.UTF8);
                MoConfigurationManager.Logger.LogInformation("配置文件添加了新节点: {SectionName}", sectionName);
                return;
            }
            
            // 获取默认配置的属性列表
            var defaultSection = defaultJsonNode[sectionName]?.AsObject();
            if (defaultSection == null) return;
            
            // 获取现有配置的属性列表
            var existingSection = sectionNode?.AsObject();
            if (existingSection == null) return;
            
            bool fileChanged = false;
            
            // 检查是否有新属性需要添加
            foreach (var property in defaultSection)
            {
                if (!existingSection.ContainsKey(property.Key))
                {
                    // 添加新属性
                    existingSection[property.Key] = property.Value?.DeepClone();
                    fileChanged = true;
                    MoConfigurationManager.Logger.LogInformation("配置文件添加了新属性: {PropertyName}", property.Key);
                }
            }
            
            // 检查是否有需要移除的属性
            if (removedPropertyHandling != RemovedPropertyHandling.Remove)
            {
                var propertiesToRemove = new List<string>();
                
                foreach (var property in existingSection)
                {
                    if (!defaultSection.ContainsKey(property.Key))
                    {
                        if (removedPropertyHandling == RemovedPropertyHandling.Comment)
                        {
                            // 注释掉被移除的属性 - 由于JSON不支持注释，我们将在序列化后的文本中手动添加注释
                            propertiesToRemove.Add(property.Key);
                        }
                        fileChanged = true;
                        MoConfigurationManager.Logger.LogInformation("发现已移除属性: {PropertyName}", property.Key);
                    }
                }
                
                // 移除需要注释的属性（为后续添加注释）
                foreach (var key in propertiesToRemove)
                {
                    existingSection.Remove(key);
                }
                
                if (fileChanged)
                {
                    // 保存更新
                    var updatedJson = jsonObject.ToJsonString(JsonSerializerOptions);
                    
                    // 如果有需要注释的属性，手动添加注释
                    if (removedPropertyHandling == RemovedPropertyHandling.Comment && propertiesToRemove.Count > 0)
                    {
                        // 序列化移除的属性以便添加为注释
                        var commentedProperties = new JsonObject();
                        foreach (var key in propertiesToRemove)
                        {
                            var originalValue = existingJsonDocument.RootElement
                                .GetProperty(sectionName)
                                .TryGetProperty(key, out var value) ? JsonNode.Parse(value.GetRawText()) : null;
                            
                            if (originalValue != null)
                            {
                                commentedProperties[key] = originalValue;
                            }
                        }
                        
                        // 将对象转换为注释文本
                        var commentJson = commentedProperties.ToJsonString(JsonSerializerOptions);
                        var commentLines = commentJson.Split('\n')
                            .Select(line => "// " + line)
                            .ToArray();
                        
                        // 在每个节的末尾添加注释
                        var lines = updatedJson.Split('\n').ToList();
                        var insertIndex = lines.FindLastIndex(line => line.Contains($"\"{sectionName}\"")) + 1;
                        
                        // 添加注释标题
                        lines.Insert(insertIndex, "  // 以下是已移除的配置项（仅供参考）:");
                        
                        // 插入注释的属性
                        foreach (var line in commentLines)
                        {
                            lines.Insert(++insertIndex, line);
                        }
                        
                        updatedJson = string.Join('\n', lines);
                    }
                    
                    File.WriteAllText(filePath, updatedJson, Encoding.UTF8);
                }
            }
            else if (fileChanged)
            {
                // 保存更新（移除模式）
                File.WriteAllText(filePath, jsonObject.ToJsonString(JsonSerializerOptions), Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            MoConfigurationManager.Logger.LogError(ex, "更新配置文件失败: {FilePath}", filePath);
        }
    }

    #endregion
}