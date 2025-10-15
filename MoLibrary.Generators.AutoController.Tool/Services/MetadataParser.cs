using System.Text.Json;
using System.Text.RegularExpressions;
using MoLibrary.Generators.AutoController.Tool.Models;

namespace MoLibrary.Generators.AutoController.Tool.Services;

/// <summary>
/// Service for parsing metadata from C# source files.
/// </summary>
public class MetadataParser
{
    // Regex pattern to extract JSON from C# 11 raw string literal
    // Matches: MetadataJson = """...JSON...""";
    private static readonly Regex JsonExtractionRegex = new(
        @"MetadataJson\s*=\s*""""""(.+?)"""""";",
        RegexOptions.Singleline | RegexOptions.Compiled
    );

    /// <summary>
    /// Parses a metadata file and extracts the JSON content along with file information.
    /// </summary>
    /// <param name="filePath">Path to the metadata file</param>
    /// <returns>MetadataFileInfo object, or null if parsing failed</returns>
    public static MetadataFileInfo? ParseMetadataFile(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                Console.WriteLine($"[WARNING] File does not exist: {filePath}");
                return null;
            }

            var content = File.ReadAllText(filePath);

            // Extract JSON from raw string literal
            var match = JsonExtractionRegex.Match(content);
            if (!match.Success)
            {
                Console.WriteLine($"[WARNING] Could not find MetadataJson in file: {filePath}");
                return null;
            }

            var jsonContent = match.Groups[1].Value.Trim();

            // Parse JSON to extract AssemblyName
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (!root.TryGetProperty("AssemblyName", out var assemblyNameElement))
            {
                Console.WriteLine($"[WARNING] AssemblyName not found in metadata: {filePath}");
                return null;
            }

            var assemblyName = assemblyNameElement.GetString();
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                Console.WriteLine($"[WARNING] AssemblyName is empty in metadata: {filePath}");
                return null;
            }

            return new MetadataFileInfo
            {
                FilePath = filePath,
                AssemblyName = assemblyName,
                JsonContent = jsonContent,
                LastWriteTime = fileInfo.LastWriteTime,
                CreationTime = fileInfo.CreationTime
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to parse file {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Writes metadata JSON to the output directory.
    /// </summary>
    /// <param name="outputDirectory">The directory to write the file to</param>
    /// <param name="metadataInfo">The metadata file information to write</param>
    public static bool WriteMetadataFile(string outputDirectory, MetadataFileInfo metadataInfo)
    {
        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputDirectory);

            // Write JSON file
            var outputPath = Path.Combine(outputDirectory, $"{metadataInfo.AssemblyName}.rpc-metadata.json");
            File.WriteAllText(outputPath, metadataInfo.JsonContent);

            Console.WriteLine($"[GENERATE] {metadataInfo.AssemblyName}.rpc-metadata.json");
            Console.WriteLine($"           Source: {metadataInfo.FilePath}");
            Console.WriteLine($"           Last Modified: {metadataInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"           Created: {metadataInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to write metadata for {metadataInfo.AssemblyName}: {ex.Message}");
            return false;
        }
    }
}
