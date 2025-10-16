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
    /// Sorts Handlers by ClientMethodName before writing.
    /// </summary>
    /// <param name="outputDirectory">The directory to write the file to</param>
    /// <param name="metadataInfo">The metadata file information to write</param>
    public static bool WriteMetadataFile(string outputDirectory, MetadataFileInfo metadataInfo)
    {
        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputDirectory);

            // Parse JSON and sort Handlers by ClientMethodName
            var sortedJsonContent = SortHandlersByClientMethodName(metadataInfo.JsonContent);

            // Write JSON file
            var outputPath = Path.Combine(outputDirectory, $"{metadataInfo.AssemblyName}.rpc-metadata.json");
            File.WriteAllText(outputPath, sortedJsonContent);

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

    /// <summary>
    /// Sorts the Handlers array in JSON by ClientMethodName.
    /// </summary>
    /// <param name="jsonContent">The original JSON content</param>
    /// <returns>JSON content with sorted Handlers</returns>
    private static string SortHandlersByClientMethodName(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Create a new JSON object with sorted Handlers
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            writer.WriteStartObject();

            // Write all properties in order
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "Handlers")
                {
                    // Sort Handlers by ClientMethodName
                    var handlers = property.Value.EnumerateArray()
                        .OrderBy(h =>
                        {
                            if (h.TryGetProperty("ClientMethodName", out var methodName))
                            {
                                return methodName.GetString() ?? string.Empty;
                            }
                            return string.Empty;
                        })
                        .ToList();

                    writer.WritePropertyName("Handlers");
                    writer.WriteStartArray();

                    foreach (var handler in handlers)
                    {
                        handler.WriteTo(writer);
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    // Write other properties as-is
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Failed to sort Handlers, using original JSON: {ex.Message}");
            return jsonContent;
        }
    }
}
