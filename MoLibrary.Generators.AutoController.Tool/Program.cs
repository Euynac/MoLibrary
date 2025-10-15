using MoLibrary.Generators.AutoController.Tool.Services;

namespace MoLibrary.Generators.AutoController.Tool;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  RPC Metadata Generator Tool");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // Load configuration
        var config = ConfigurationService.LoadConfiguration();
        if (config == null)
        {
            Console.WriteLine("[EXIT] Please configure appsettings.json and run again.");
            return 1;
        }

        // Validate configuration
        if (!config.IsValid(out var errors))
        {
            Console.WriteLine("[ERROR] Configuration validation failed:");
            foreach (var error in errors)
            {
                Console.WriteLine($"  - {error}");
            }
            return 1;
        }

        // Resolve paths
        var contextDir = ConfigurationService.ResolvePath(config.ContextDirectory);
        var outputDir = ConfigurationService.ResolvePath(config.OutputDirectory);

        Console.WriteLine($"[CONFIG] Context Directory: {contextDir}");
        Console.WriteLine($"[CONFIG] Output Directory: {outputDir}");
        Console.WriteLine($"[CONFIG] Metadata File Name: {config.MetadataFileName}");
        Console.WriteLine();

        // Scan for metadata files
        var metadataFiles = MetadataScanner.ScanForMetadataFiles(contextDir, config.MetadataFileName);
        if (metadataFiles.Count == 0)
        {
            Console.WriteLine("[WARNING] No metadata files found.");
            return 0;
        }

        Console.WriteLine();

        // Parse all metadata files
        var parsedMetadataList = new List<Models.MetadataFileInfo>();
        int parseFailureCount = 0;

        foreach (var filePath in metadataFiles)
        {
            var metadataInfo = MetadataParser.ParseMetadataFile(filePath);
            if (metadataInfo == null)
            {
                parseFailureCount++;
                continue;
            }
            parsedMetadataList.Add(metadataInfo);
        }

        if (parsedMetadataList.Count == 0)
        {
            Console.WriteLine("[WARNING] No valid metadata files found after parsing.");
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine($"[SUMMARY] Processed {metadataFiles.Count} file(s)");
            Console.WriteLine($"  ✗ Failed: {parseFailureCount}");
            Console.WriteLine("========================================");
            return parseFailureCount > 0 ? 1 : 0;
        }

        // Group by AssemblyName and detect duplicates
        var groupedMetadata = parsedMetadataList
            .GroupBy(m => m.AssemblyName)
            .ToList();

        var metadataToGenerate = new List<Models.MetadataFileInfo>();
        int duplicateCount = 0;

        foreach (var group in groupedMetadata)
        {
            if (group.Count() > 1)
            {
                // Found duplicates - select the one with the latest LastWriteTime
                var sorted = group.OrderByDescending(m => m.LastWriteTime).ToList();
                var latest = sorted.First();
                metadataToGenerate.Add(latest);
                duplicateCount += group.Count() - 1;

                Console.WriteLine($"[DUPLICATE] Found {group.Count()} files for AssemblyName: {group.Key}");
                Console.WriteLine($"            Using latest: {latest.FilePath}");
                Console.WriteLine($"            Last Modified: {latest.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

                for (int i = 1; i < sorted.Count; i++)
                {
                    Console.WriteLine($"            Skipping: {sorted[i].FilePath}");
                    Console.WriteLine($"            Last Modified: {sorted[i].LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                }
                Console.WriteLine();
            }
            else
            {
                metadataToGenerate.Add(group.First());
            }
        }

        // Generate metadata files
        int successCount = 0;
        int failureCount = 0;

        foreach (var metadataInfo in metadataToGenerate)
        {
            if (MetadataParser.WriteMetadataFile(outputDir, metadataInfo))
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
            Console.WriteLine();
        }

        Console.WriteLine("========================================");
        Console.WriteLine($"[SUMMARY] Processed {metadataFiles.Count} file(s)");
        Console.WriteLine($"  ✓ Generated: {successCount}");
        if (duplicateCount > 0)
        {
            Console.WriteLine($"  ⚠ Duplicates Skipped: {duplicateCount}");
        }
        if (parseFailureCount > 0)
        {
            Console.WriteLine($"  ✗ Parse Failed: {parseFailureCount}");
        }
        if (failureCount > 0)
        {
            Console.WriteLine($"  ✗ Generation Failed: {failureCount}");
        }
        Console.WriteLine("========================================");

        return (failureCount + parseFailureCount) > 0 ? 1 : 0;
    }
}
