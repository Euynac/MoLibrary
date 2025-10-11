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

        // Process each metadata file
        int successCount = 0;
        int failureCount = 0;

        foreach (var filePath in metadataFiles)
        {
            var result = MetadataParser.ParseMetadataFile(filePath);
            if (result == null)
            {
                failureCount++;
                continue;
            }

            var (assemblyName, jsonContent) = result.Value;
            if (MetadataParser.WriteMetadataFile(outputDir, assemblyName, jsonContent))
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine($"[SUMMARY] Processed {metadataFiles.Count} file(s)");
        Console.WriteLine($"  ✓ Success: {successCount}");
        if (failureCount > 0)
        {
            Console.WriteLine($"  ✗ Failed: {failureCount}");
        }
        Console.WriteLine("========================================");

        return failureCount > 0 ? 1 : 0;
    }
}
