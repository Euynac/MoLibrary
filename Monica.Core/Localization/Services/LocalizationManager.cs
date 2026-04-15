using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core.Localization.Abstractions;
using Monica.Core.Localization.Models;
using Monica.Core.Localization.Models.Internal;
using Monica.Core.Localization.Services.Support;
using Monica.Core.Logging;

namespace Monica.Core.Localization.Services;

/// <summary>
/// Provides DI-free access to Monica localization resources.
/// This manager can be used before the DI container is built and continues to work after the localization module configures runtime services.
/// </summary>
public static class LocalizationManager
{
    private static readonly Lock SyncRoot = new();
    private static readonly ConcurrentDictionary<Type, IStringLocalizer> LocalizerCache = new();

    private static LocalizationManagerOptions _options = new();
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Gets a copy of the current localization manager options.
    /// </summary>
    public static LocalizationManagerOptions Options
    {
        get
        {
            lock (SyncRoot)
            {
                return _options.Clone();
            }
        }
    }

    /// <summary>
    /// Applies a new options snapshot to the localization manager.
    /// Existing cached localizers are refreshed only when the effective options change.
    /// </summary>
    /// <param name="options">The options to apply.</param>
    public static void UseOptions(LocalizationManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalizedOptions = NormalizeOptions(options);

        lock (SyncRoot)
        {
            if (OptionsEqual(_options, normalizedOptions))
            {
                return;
            }

            _options = normalizedOptions;
            LocalizerCache.Clear();
        }
    }

    /// <summary>
    /// Mutates the current options snapshot.
    /// Existing cached localizers are refreshed only when the effective options change.
    /// </summary>
    /// <param name="configure">The callback used to mutate a cloned options object.</param>
    public static void Configure(Action<LocalizationManagerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var clonedOptions = Options;
        configure(clonedOptions);
        UseOptions(clonedOptions);
    }

    /// <summary>
    /// Overrides the logger factory used for newly created localizers.
    /// Existing cached localizers are refreshed only when the factory instance changes.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use.</param>
    public static void UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        lock (SyncRoot)
        {
            if (ReferenceEquals(_loggerFactory, loggerFactory))
            {
                return;
            }

            _loggerFactory = loggerFactory;
            LocalizerCache.Clear();
        }
    }

    /// <summary>
    /// Creates or retrieves a cached localizer for the specified localization resource marker type.
    /// </summary>
    /// <typeparam name="TResource">The localization resource marker type.</typeparam>
    /// <returns>The localizer for the specified resource.</returns>
    public static IStringLocalizer For<TResource>() where TResource : class, ILocalizationResource
    {
        return For(typeof(TResource));
    }

    /// <summary>
    /// Creates or retrieves a cached localizer for the specified localization resource marker type.
    /// </summary>
    /// <param name="resourceType">The localization resource marker type.</param>
    /// <returns>The localizer for the specified resource.</returns>
    public static IStringLocalizer For(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        EnsureResourceMarkerType(resourceType);

        return LocalizerCache.GetOrAdd(resourceType, static type => CreateLocalizer(LocalizationResourceRegistration.Create(type)));
    }

    /// <summary>
    /// Resolves a localized string for the specified resource marker type.
    /// </summary>
    /// <typeparam name="TResource">The localization resource marker type.</typeparam>
    /// <param name="key">The localization key.</param>
    /// <returns>The localized value, or the key when no value is available.</returns>
    public static string Get<TResource>(string key) where TResource : class, ILocalizationResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return For<TResource>()[key].Value;
    }

    /// <summary>
    /// Resolves and formats a localized string for the specified resource marker type.
    /// </summary>
    /// <typeparam name="TResource">The localization resource marker type.</typeparam>
    /// <param name="key">The localization key.</param>
    /// <param name="arguments">The formatting arguments.</param>
    /// <returns>The localized and formatted value, or the key when no value is available.</returns>
    public static string Get<TResource>(string key, params object[] arguments) where TResource : class, ILocalizationResource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return For<TResource>()[key, arguments].Value;
    }

    internal static IStringLocalizer CreateEmptyLocalizer(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        var options = SnapshotOptions();
        return new DictionaryStringLocalizer(
            resourceType.Name,
            [],
            options,
            SnapshotLoggerFactory().CreateLogger<DictionaryStringLocalizer>());
    }

    internal static IStringLocalizer CreateLocalizer(LocalizationResourceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var options = SnapshotOptions();
        var resources = EmbeddedJsonResourceLoader.Load(registration, options.SupportedCultures);

        return new DictionaryStringLocalizer(
            registration.ResourceType.Name,
            resources,
            options,
            SnapshotLoggerFactory().CreateLogger<DictionaryStringLocalizer>());
    }

    private static void EnsureResourceMarkerType(Type resourceType)
    {
        if (!typeof(ILocalizationResource).IsAssignableFrom(resourceType))
        {
            throw new ArgumentException(
                $"Resource type '{resourceType.FullName ?? resourceType.Name}' must implement {nameof(ILocalizationResource)}.",
                nameof(resourceType));
        }
    }

    private static LocalizationManagerOptions SnapshotOptions()
    {
        lock (SyncRoot)
        {
            return _options.Clone();
        }
    }

    private static ILoggerFactory SnapshotLoggerFactory()
    {
        lock (SyncRoot)
        {
            return _loggerFactory ?? LogManager.Factory;
        }
    }

    private static LocalizationManagerOptions NormalizeOptions(LocalizationManagerOptions options)
    {
        var defaultCulture = NormalizeCulture(options.DefaultCulture, nameof(options.DefaultCulture));

        var supportedCultures = options.SupportedCultures
            .Select(culture => NormalizeCulture(culture, nameof(options.SupportedCultures)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (supportedCultures.Count == 0)
        {
            supportedCultures.Add(defaultCulture);
        }
        else if (!supportedCultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            supportedCultures.Add(defaultCulture);
        }

        return new LocalizationManagerOptions
        {
            DefaultCulture = defaultCulture,
            SupportedCultures = supportedCultures
        };
    }

    private static bool OptionsEqual(LocalizationManagerOptions left, LocalizationManagerOptions right)
    {
        return string.Equals(left.DefaultCulture, right.DefaultCulture, StringComparison.OrdinalIgnoreCase) &&
               left.SupportedCultures.SequenceEqual(right.SupportedCultures, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeCulture(string cultureName, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName, parameterName);
        return CultureInfo.GetCultureInfo(cultureName.Trim()).Name;
    }
}
