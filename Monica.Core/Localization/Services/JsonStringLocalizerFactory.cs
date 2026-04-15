using System.Collections.Concurrent;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Localization.Services.Support;
using Monica.Modules;

namespace Monica.Core.Localization.Services;

internal sealed class JsonStringLocalizerFactory(
    IOptions<ModuleLocalizationOption> options,
    ILoggerFactory loggerFactory,
    LocalizationResourceRegistry resourceRegistry) : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<Type, IStringLocalizer> _localizerCache = new();
    private readonly ILogger<JsonStringLocalizerFactory> _logger = loggerFactory.CreateLogger<JsonStringLocalizerFactory>();

    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);

        LocalizationManager.UseOptions(options.Value.ToManagerOptions());
        LocalizationManager.UseLoggerFactory(loggerFactory);

        return _localizerCache.GetOrAdd(resourceSource, CreateLocalizer);
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        // Not used in Monica - all resources are type-based
        throw new NotSupportedException("String-based localization is not supported. Use IStringLocalizer<T> instead.");
    }

    private IStringLocalizer CreateLocalizer(Type resourceSource)
    {
        if (resourceRegistry.TryGetRegistration(resourceSource, out _))
        {
            return LocalizationManager.For(resourceSource);
        }

        _logger.LogDebug(
            "No localization resource registration was found for {ResourceType}. Returning an empty localizer.",
            resourceSource.FullName ?? resourceSource.Name);

        return LocalizationManager.CreateEmptyLocalizer(resourceSource);
    }
}
