namespace MoLibrary.Generators.AutoController.Tool.Services;

/// <summary>
/// Service for scanning directories to find metadata files.
/// </summary>
public class MetadataScanner
{
    /// <summary>
    /// Scans a directory recursively for files matching the specified name.
    /// </summary>
    /// <param name="contextDirectory">The root directory to search</param>
    /// <param name="fileName">The file name to search for</param>
    /// <returns>List of absolute paths to matching files</returns>
    public static List<string> ScanForMetadataFiles(string contextDirectory, string fileName)
    {
        var results = new List<string>();

        if (!Directory.Exists(contextDirectory))
        {
            Console.WriteLine($"[WARNING] Context directory does not exist: {contextDirectory}");
            return results;
        }

        try
        {
            // Search for files recursively
            var files = Directory.GetFiles(contextDirectory, fileName, SearchOption.AllDirectories);
            results.AddRange(files);

            Console.WriteLine($"[SCAN] Found {results.Count} metadata file(s) in: {contextDirectory}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to scan directory: {ex.Message}");
        }

        return results;
    }
}
