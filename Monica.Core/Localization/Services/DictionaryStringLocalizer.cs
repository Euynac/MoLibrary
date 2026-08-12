using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core.Localization.Models;

namespace Monica.Core.Localization.Services;

internal sealed class DictionaryStringLocalizer(
    string resourceName,
    Dictionary<string, Dictionary<string, string>> resources,
    LocalizationProfile profile,
    ILogger<DictionaryStringLocalizer> logger) : IStringLocalizer
{
    public LocalizedString this[string name]
    {
        get
        {
            var value = GetString(name);
            return new LocalizedString(name, value ?? name, value == null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = GetString(name);
            if (value == null)
            {
                return new LocalizedString(name, name, true);
            }

            try
            {
                var formatted = string.Format(value, arguments);
                return new LocalizedString(name, formatted, false);
            }
            catch (FormatException ex)
            {
                logger.LogWarning(ex, "Format error for key '{Key}' in resource '{ResourceName}'", name, resourceName);
                return new LocalizedString(name, value, false);
            }
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var culture = CultureInfo.CurrentUICulture.Name;

        if (resources.TryGetValue(culture, out var strings))
        {
            foreach (var (key, value) in strings)
            {
                yield return new LocalizedString(key, value, false);
            }
        }
    }

    private string? GetString(string name)
    {
        var culture = CultureInfo.CurrentUICulture;

        // Fallback chain: exact culture → base culture → default culture → null
        var culturesToTry = new List<string>();

        // 1. Exact culture (e.g., zh-CN)
        culturesToTry.Add(culture.Name);

        // 2. Base culture (e.g., zh)
        if (!culture.IsNeutralCulture && culture.Parent != CultureInfo.InvariantCulture)
        {
            culturesToTry.Add(culture.Parent.Name);
        }

        // 3. Default culture
        if (profile.DefaultCulture != culture.Name)
        {
            culturesToTry.Add(profile.DefaultCulture);
        }

        foreach (var cultureToTry in culturesToTry)
        {
            if (resources.TryGetValue(cultureToTry, out var strings) &&
                strings.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return null;
    }
}
