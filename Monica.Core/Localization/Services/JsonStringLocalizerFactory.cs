using System.Collections.Concurrent;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core.Localization.Models;
using Monica.Core.Localization.Services.Support;

namespace Monica.Core.Localization.Services;

internal sealed class JsonStringLocalizerFactory(
    LocalizationProfile profile,
    LocalizationResourceRegistry resourceRegistry,
    ILogger<JsonStringLocalizerFactory> logger,
    ILogger<DictionaryStringLocalizer> localizerLogger) : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<Type, IStringLocalizer> _localizerCache = new();

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
            var resources = EmbeddedJsonResourceLoader.Load(registration, profile.SupportedCultures);
            return new DictionaryStringLocalizer(resourceSource.Name, resources, profile, localizerLogger);
        }

        logger.LogDebug(
            "No localization resource registration was found for {ResourceType}. Returning an empty localizer.",
            resourceSource.FullName ?? resourceSource.Name);

        return new DictionaryStringLocalizer(resourceSource.Name, [], profile, localizerLogger);
    }
}
