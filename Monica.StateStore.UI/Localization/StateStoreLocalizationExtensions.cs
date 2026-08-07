using Microsoft.Extensions.Localization;
using Monica.StateStore.Abstractions;
using Monica.StateStore.UI.Models;

namespace Monica.StateStore.UI.Localization;

/// <summary>
/// Provides localized display text for StateStore provider diagnostics.
/// </summary>
public static class StateStoreLocalizationExtensions
{
    /// <summary>Gets the localized registration name for a StateStore provider.</summary>
    public static string GetProviderDisplayName(
        this IStringLocalizer localizer,
        StateStoreProviderInfo provider)
    {
        return string.IsNullOrWhiteSpace(provider.ServiceKey)
            ? localizer["KeyExplorer:Labels:DefaultProvider"].Value
            : provider.ServiceKey;
    }

    /// <summary>Gets the localized name of a StateStore provider family.</summary>
    public static string GetProviderTypeText(
        this IStringLocalizer localizer,
        EStateStoreProviderType providerType)
    {
        return providerType switch
        {
            EStateStoreProviderType.Memory => localizer["ProviderDetail:ProviderTypes:Memory"].Value,
            EStateStoreProviderType.Redis => localizer["ProviderDetail:ProviderTypes:Redis"].Value,
            EStateStoreProviderType.Dapr => localizer["ProviderDetail:ProviderTypes:Dapr"].Value,
            _ => localizer["ProviderDetail:ProviderTypes:Unknown"].Value
        };
    }
}
