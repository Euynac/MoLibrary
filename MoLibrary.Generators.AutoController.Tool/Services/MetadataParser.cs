using System.Text.Json;
using System.Text.RegularExpressions;

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
    /// Parses a metadata file and extracts the JSON content.
    /// </summary>
    /// <param name="filePath">Path to the metadata file</param>
    /// <returns>Tuple of (AssemblyName, JsonContent), or null if parsing failed</returns>
    public static (string AssemblyName, string JsonContent)? ParseMetadataFile(string filePath)
    {
        try
        {
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

            return (assemblyName, jsonContent);
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
    /// <param name="assemblyName">The assembly name (used as filename)</param>
    /// <param name="jsonContent">The JSON content to write</param>
    public static bool WriteMetadataFile(string outputDirectory, string assemblyName, string jsonContent)
    {
        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputDirectory);

            // Write JSON file
            var outputPath = Path.Combine(outputDirectory, $"{assemblyName}.rpc-metadata.json");
            File.WriteAllText(outputPath, jsonContent);

            Console.WriteLine($"[GENERATE] {assemblyName}.rpc-metadata.json");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to write metadata for {assemblyName}: {ex.Message}");
            return false;
        }
    }
}
