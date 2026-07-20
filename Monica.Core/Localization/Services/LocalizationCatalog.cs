using Microsoft.Extensions.Localization;
using Monica.Core.Localization.Abstractions;

namespace Monica.Core.Localization.Services;

internal sealed class LocalizationCatalog(IStringLocalizerFactory localizerFactory) : ILocalizationCatalog
{
    public IStringLocalizer For<TResource>() where TResource : class, ILocalizationResource
    {
        return localizerFactory.Create(typeof(TResource));
    }

    public IStringLocalizer For(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        if (!typeof(ILocalizationResource).IsAssignableFrom(resourceType))
        {
            throw new ArgumentException(
                $"Resource type '{resourceType.FullName ?? resourceType.Name}' must implement {nameof(ILocalizationResource)}.",
                nameof(resourceType));
        }

        return localizerFactory.Create(resourceType);
    }

    public string Get<TResource>(string key) where TResource : class, ILocalizationResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return For<TResource>()[key].Value;
    }

    public string Get<TResource>(string key, params object[] arguments)
        where TResource : class, ILocalizationResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(arguments);
        return For<TResource>()[key, arguments].Value;
    }
}
