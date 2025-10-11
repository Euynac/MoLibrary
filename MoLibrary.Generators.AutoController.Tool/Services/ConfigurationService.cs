using System.Text.Json;
using MoLibrary.Generators.AutoController.Tool.Models;

namespace MoLibrary.Generators.AutoController.Tool.Services;

/// <summary>
/// Service for managing tool configuration.
/// </summary>
public class ConfigurationService
{
    private const string ConfigFileName = "appsettings.json";
    private const string TemplateFileName = "appsettings.template.json";

    /// <summary>
    /// Loads configuration from appsettings.json.
    /// If the file doesn't exist, creates it from the template.
    /// </summary>
    public static ToolConfig? LoadConfiguration()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);

        // Check if configuration exists
        if (!File.Exists(configPath))
        {
            CreateConfigurationFromTemplate(configPath);
            return null;
        }

        // Load and parse configuration
        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ToolConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load configuration: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a new configuration file from the template.
    /// </summary>
    private static void CreateConfigurationFromTemplate(string configPath)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, TemplateFileName);

        if (!File.Exists(templatePath))
        {
            Console.WriteLine($"[ERROR] Template file not found: {templatePath}");
            return;
        }

        try
        {
            File.Copy(templatePath, configPath);
            Console.WriteLine($"[INFO] Configuration file created: {configPath}");
            Console.WriteLine("[ACTION] Please edit appsettings.json and configure the required paths.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to create configuration file: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves a path (absolute or relative) to an absolute path.
    /// </summary>
    public static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        // Relative path - resolve from current directory
        var currentDir = Directory.GetCurrentDirectory();
        var resolvedPath = Path.GetFullPath(Path.Combine(currentDir, path));
        return resolvedPath;
    }
}
