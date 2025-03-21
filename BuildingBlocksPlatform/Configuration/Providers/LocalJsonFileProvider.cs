using BuildingBlocksPlatform.Configuration.Model;
using Koubot.Tool.General;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocksPlatform.Configuration.Providers;

/// <summary>
/// 本地Json配置文件提供者
/// </summary>
/// <param name="card"></param>
public class LocalJsonFileProvider(MoConfigurationCard card)
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new() { WriteIndented = true };
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


        //TODO 未处理文件内容不合法的情况
        var path = GeneralExtensions.GetRelativePathInRunningPath(filename);
        if (!File.Exists(path))
        {
            var directory = FileTool.GetDirectoryPath(path)!;
            Directory.CreateDirectory(directory);
            var contents = GetDefaultFileContents();
            File.WriteAllText(path, contents, Encoding.UTF8);
        }

        ((ConfigurationManager)MoConfigurationManager.AppConfiguration).AddJsonFile(path, false, true);
    }

    #endregion
}