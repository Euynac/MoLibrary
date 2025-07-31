using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Model;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Interfaces;

public class MoConfigurationJsonFileModifier(ILogger<MoConfigurationJsonFileModifier> logger) : IMoConfigurationModifier
{
    public async Task<Res<OptionItem>> IsOptionExist(string key)
    {
        if (!MoConfigurationCard.TryGetOptionItem(key, out var option))
        {
            return $"无法找到{key}所对应的配置项";
        }

        return option;
    }

    public async Task<Res<DtoUpdateConfigRes>> UpdateOption(string key, JsonNode? value)
    {
        if ((await IsOptionExist(key)).IsFailed(out var fail, out var option)) return fail;
        return await UpdateOption(option, value);
    }

    public async Task<Res<DtoUpdateConfigRes>> UpdateOption(OptionItem option, JsonNode? value)
    {
        var key = option.Key;
        if (option.Provider?.Equals(nameof(JsonConfigurationProvider)) is false)
        {
            return $"暂不支持更新Provider为{option.Provider}的配置项";
        }

        if (option.Source == null)
        {
            var error = $"配置更新失败：无法获取其Json文件来源。更新操作：{key} => {value}";
            logger.LogError(error);
            return error;
        }

        try
        {
            var configKey = key.Split(":").First();//获取配置类Key
            if ((await IsConfigExist(configKey)).IsFailed(out var fail, out var config)) return fail;


            var doc = new JsonSettingsDocument(option.Source);
            var oldValue = JsonSettingsDocument.CloneJsonNode(doc[configKey]);
            doc[key] = value;
            doc.Save(option.Source);
            var newValue = doc[configKey];
            
            return new DtoUpdateConfigRes()
            {
                Title = config.Info.Title ?? config.Name,
                Key = configKey,
                NewValue = newValue,
                OldValue = oldValue,
            };
        }
        catch (Exception e)
        {
            var error = $"配置更新失败：{e.Message}。更新操作：{key} => {value}";
            logger.LogError(error);
            return error;
        }
    }

    public async Task<Res<MoConfiguration>> IsConfigExist(string key)
    {
        if (!MoConfigurationCard.TryGetConfig(key, out var option))
        {
            return $"无法找到{key}所对应的配置类";
        }

        return option;
    }

    public async Task<Res<DtoUpdateConfigRes>> UpdateConfig(string key, JsonNode? value)
    {
        if ((await IsConfigExist(key)).IsFailed(out var fail, out var config)) return fail;
        return await UpdateConfig(config, value);
    }

    public async Task<Res<DtoUpdateConfigRes>> UpdateConfig(MoConfiguration config, JsonNode? value)
    {
        var key = config.Key;
        var option = config.OptionItems.FirstOrDefault();
        if (option == null)
        {
            return $"配置类{key}中找不到任何配置项";
        }
        if (option.Provider?.Equals(nameof(JsonConfigurationProvider)) is false)
        {
            return $"暂不支持更新Provider为{option.Provider}的配置项";
        }

        if (option.Source == null)
        {
            var error = $"配置更新失败：无法获取其Json文件来源。更新操作：{key} => {value}";
            logger.LogError(error);
            return error;
        }

        try
        {
            var doc = new JsonSettingsDocument(option.Source);
            var oldValue = JsonSettingsDocument.CloneJsonNode(doc[key]);
            doc[key] = value;
            doc.Save(option.Source);
            var newValue = doc[key];
            return new DtoUpdateConfigRes()
            {
                Title = config.Info.Title ?? config.Name,
                Key = key,
                NewValue = newValue,
                OldValue = oldValue
            };
        }
        catch (Exception e)
        {
            var error = $"配置更新失败：{e.Message}。更新操作：{key} => {value}";
            logger.LogError(error);
            return error;
        }
    }
}


internal class JsonSettingsDocument
{
    private JsonNode? _doc;

    public JsonNode? this[string key] { get => GetValue(key); set => SetValue(key, value); }

    private readonly string _filePath;

    public JsonSettingsDocument(string path)
    {
        _filePath = path;
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        _doc = JsonNode.Parse(file,
            new JsonNodeOptions {PropertyNameCaseInsensitive = true},
            new JsonDocumentOptions {CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true});

    }

    private JsonNode? GetValue(string key)
    {
        return GetParentNode(key, false, out var targetProperty)?[targetProperty];
    }

    private void SetValue(string key, JsonNode? value, bool notExistThenCreate = false)
    {
        var node = GetParentNode(key, notExistThenCreate, out var targetProperty);
        if (node == null)
        {
            throw new InvalidOperationException($"{_filePath}不存在{key}的Json Node");
        }

        var obj = node.AsObject();
        
        if (!obj.ContainsKey(targetProperty)) 
        {
            if (!notExistThenCreate)
            {
                throw new InvalidOperationException($"{_filePath}不存在{key}的Json Node");
            }

            node[targetProperty] = value;
            return;
        }

        var targetNode = obj[targetProperty];
        //TODO 当修改配置类时，递归判断类型是否正确
        //当jsonNode值为null时，获取到的是null。
        if (!IsJsonNodeValueKindCompatible(targetNode, value))
        {
            throw new InvalidOperationException($"{_filePath}中{key}的JsonNode类型{targetNode?.GetValueKind()}与将修改成为的类型{value?.GetValueKind()}不一致");
        }
        node[targetProperty] = value;
    }

    public static JsonNode? CloneJsonNode(JsonNode? node)
    {
        return JsonSerializer.Deserialize<JsonNode?>(JsonSerializer.Serialize(node));
    }

    private bool IsJsonNodeValueKindCompatible(JsonNode? targetNode, JsonNode? newNode)
    {
        var targetKind = targetNode?.GetValueKind() ?? JsonValueKind.Null;
        var newKind = newNode?.GetValueKind() ?? JsonValueKind.Null;
        if (targetKind == JsonValueKind.Null || newKind == JsonValueKind.Null) return true;

        if (targetKind.EqualsAny(JsonValueKind.True, JsonValueKind.False) &&
            newKind.EqualsAny(JsonValueKind.True, JsonValueKind.False))
        {
            return true;
        }


        return targetKind == newKind;
    }


    private JsonNode? GetParentNode(string key, bool create, out string targetProperty)
    {
        var props = key.Split(':');
        targetProperty = props[^1];
        _doc ??= new JsonObject();
        var parent = _doc.Root;
        for (var i = 0; i < props.Length - 1; i++)
        {
            var node = parent[props[i]];
            if (node is null)
            {
                if (create)
                {
                    node = new JsonObject();
                    parent[props[i]] = node;
                }
                else
                {
                    return null;
                }
            }
            parent = node;
        }
        return parent;
    }

    public void Save(string path)
    {
        using FileStream file = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using Utf8JsonWriter jsonWriter = new(file, new JsonWriterOptions()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        // Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 可以防止将Unicode的中文等字符变为\u的形式。虽然不用也不影响解析。 官方使用Default的Encoder主要是为了安全？
        _doc ??= new JsonObject();
        _doc.WriteTo(jsonWriter);
    }

}