using System.Collections.Concurrent;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Localization.Json;
using Monica.Core.Localization.Models;
using Monica.Modules;

namespace Monica.Core.Localization.Localizers;

internal class MoStringLocalizerFactory(
    IOptions<ModuleLocalizationOption> options,
    ILoggerFactory loggerFactory,
    LocalizationResourceRegistry resourceRegistry) : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<Type, IStringLocalizer> _localizerCache = new();
    private readonly ILogger<MoStringLocalizerFactory> _logger = loggerFactory.CreateLogger<MoStringLocalizerFactory>();

    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);

        return _localizerCache.GetOrAdd(resourceSource, CreateLocalizer);
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        // Not used in Monica - all resources are type-based
        throw new NotSupportedException("String-based localization is not supported. Use IStringLocalizer<T> instead.");
    }

    private IStringLocalizer CreateLocalizer(Type resourceSource)
    {
        if (resourceRegistry.TryGetRegistration(resourceSource, out var registration))
        {
            return CreateDictionaryLocalizer(registration);
        }

        _logger.LogDebug(
            "No localization resource registration was found for {ResourceType}. Returning an empty localizer.",
            resourceSource.FullName ?? resourceSource.Name);

        return CreateEmptyLocalizer(resourceSource);
    }

    private IStringLocalizer CreateDictionaryLocalizer(LocalizationResourceRegistration registration)
    {
        var resources = JsonResourceLoader.Load(registration, options.Value.SupportedCultures);

        _logger.LogDebug(
            "Loaded localization resources for {ResourceType} from {Assembly}. Culture count: {CultureCount}",
            registration.ResourceType.FullName ?? registration.ResourceType.Name,
            registration.Assembly.GetName().Name,
            resources.Count);

        return new MoDictionaryStringLocalizer(
            registration.ResourceType.Name,
            resources,
            options.Value,
            loggerFactory.CreateLogger<MoDictionaryStringLocalizer>());
    }

    private IStringLocalizer CreateEmptyLocalizer(Type resourceSource)
    {
        return new MoDictionaryStringLocalizer(
            resourceSource.Name,
            new Dictionary<string, Dictionary<string, string>>(),
            options.Value,
            loggerFactory.CreateLogger<MoDictionaryStringLocalizer>());
    }
}
