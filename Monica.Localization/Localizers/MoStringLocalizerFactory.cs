using System.Collections.Concurrent;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Localization.Json;
using Monica.Modules;

namespace Monica.Localization.Localizers;

public class MoStringLocalizerFactory(
    IOptions<ModuleLocalizationOption> options,
    ILoggerFactory loggerFactory) : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<Type, IStringLocalizer> _localizerCache = new();
    private readonly Lazy<Dictionary<Type, Dictionary<string, Dictionary<string, string>>>> _allResources
        = new(() => LoadAllResources(options.Value, loggerFactory.CreateLogger<MoStringLocalizerFactory>()));

    public IStringLocalizer Create(Type resourceSource)
    {
        return _localizerCache.GetOrAdd(resourceSource, type =>
        {
            if (_allResources.Value.TryGetValue(type, out var resources))
            {
                return new MoDictionaryStringLocalizer(
                    type.Name,
                    resources,
                    options.Value,
                    loggerFactory.CreateLogger<MoDictionaryStringLocalizer>());
            }

            // Fallback: return empty localizer that returns keys
            return new MoDictionaryStringLocalizer(
                type.Name,
                new Dictionary<string, Dictionary<string, string>>(),
                options.Value,
                loggerFactory.CreateLogger<MoDictionaryStringLocalizer>());
        });
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        // Not used in Monica - all resources are type-based
        throw new NotSupportedException("String-based localization is not supported. Use IStringLocalizer<T> instead.");
    }

    private static Dictionary<Type, Dictionary<string, Dictionary<string, string>>> LoadAllResources(
        ModuleLocalizationOption option,
        ILogger logger)
    {
        var result = new Dictionary<Type, Dictionary<string, Dictionary<string, string>>>();

        // Auto-discover resources from all loaded application assemblies so third-party Monica modules
        // can ship their own localization resources without living in a Monica.* assembly.
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(ShouldScanAssembly);

        foreach (var assembly in assemblies)
        {
            try
            {
                var resourceTypes = assembly.GetExportedTypes()
                    .Where(t => t.Namespace?.Contains(".Localization") == true &&
                               t.IsClass &&
                               !t.IsAbstract);

                foreach (var resourceType in resourceTypes)
                {
                    var resources = JsonResourceLoader.LoadFromAssembly(
                        assembly,
                        resourceType,
                        option.SupportedCultures);

                    if (resources.Count > 0)
                    {
                        result[resourceType] = resources;
                        logger.LogDebug("Loaded localization resources for {ResourceType} from {Assembly}",
                            resourceType.Name, assembly.GetName().Name);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load localization resources from assembly {Assembly}",
                    assembly.GetName().Name);
            }
        }

        logger.LogInformation("Loaded localization resources for {Count} resource types", result.Count);
        return result;
    }

    private static bool ShouldScanAssembly(System.Reflection.Assembly assembly)
    {
        if (assembly.IsDynamic)
        {
            return false;
        }

        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        return !ExcludedAssemblyPrefixes.Any(prefix =>
            assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] ExcludedAssemblyPrefixes =
    [
        "System",
        "Microsoft",
        "mscorlib",
        "netstandard",
        "Windows",
        "Presentation",
        "Accessibility",
        "MudBlazor",
        "Serilog",
        "Renci",
        "Npgsql",
        "Google",
        "Grpc",
        "Swashbuckle"
    ];
}
