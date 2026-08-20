using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Localization;
using Monica.Core.Localization.Abstractions;
using Monica.Core.Localization.Models;
using Monica.Core.Localization.Services;
using Monica.Core.Localization.Services.Support;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.TypeDiscovery.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLocalizationBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure Localization module
        /// </summary>
        public ModuleRegistration<ModuleLocalization, ModuleLocalizationOption> AddLocalization(
            Action<ModuleLocalizationOption>? action = null)
        {
            return builder.AddModule<ModuleLocalization, ModuleLocalizationOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleLocalization, ModuleLocalizationOption> registration)
    {
        /// <summary>
        /// Registers a localization resource marker that is outside the host's business-type scan.
        /// </summary>
        public ModuleRegistration<ModuleLocalization, ModuleLocalizationOption> AddResource<TResource>()
            where TResource : class, ILocalizationResource
        {
            return registration.Configure(static option => option.AddResource<TResource>());
        }
    }
}

public class ModuleLocalization : MonicaModule<ModuleLocalizationOption>, IWebModule
{
    private readonly LocalizationResourceRegistry _resourceRegistry = new();
    private readonly List<Type> _resourceMarkerTypesDiscoveredFromTypeScan = [];

    /// <inheritdoc />
    public override void ValidateOptions(ModuleLocalizationOption options, string? profileName)
    {
        _ = options.CreateProfile();
    }

    public override void ConfigureApplicationBuilder(WebModuleContext<ModuleLocalizationOption> context)
    {
        context.ApplicationBuilder.UseRequestLocalization();
    }

    public override void ConfigureServices(ModuleContext<ModuleLocalizationOption> context)
    {
        var services = context.Services;
        var profile = Option.CreateProfile();

        // Add ASP.NET Core localization services
        services.AddLocalization();
        services.TryAddSingleton(_resourceRegistry);
        services.AddSingleton(profile);
        services.TryAddSingleton<JsonStringLocalizerFactory>();
        services.TryAddSingleton<ILocalizationCatalog, LocalizationCatalog>();

        // Replace default factory with custom JSON-based factory
        services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory>(serviceProvider =>
            serviceProvider.GetRequiredService<JsonStringLocalizerFactory>()));

        // Configure RequestLocalizationOptions
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = profile.SupportedCultures
                .Select(c => new CultureInfo(c))
                .ToArray();

            options.DefaultRequestCulture = new RequestCulture(profile.DefaultCulture);
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            // Cookie provider for culture persistence
            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider
                {
                    CookieName = profile.CookieName
                }
            ];
        });
    }

    /// <summary>
    /// Collects localization resource marker types discovered through the global type finder.
    /// This path is intended for resource types that live in host or business assemblies participating in the configured scan.
    /// Built-in Monica modules must not rely on this hook because their assemblies may not be part of that scan.
    /// Monica modules should declare a dependency on <see cref="ModuleLocalization"/> and register reusable resource types through the registration extension.
    /// </summary>
    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ModuleLocalizationOption> discovery)
    {
        discovery.Match(
            TypeQuery.ConcreteClass.AssignableTo<ILocalizationResource>(),
            (_, matches) =>
            {
                _resourceMarkerTypesDiscoveredFromTypeScan.Clear();
                _resourceMarkerTypesDiscoveredFromTypeScan.AddRange(
                    matches.Select(static match => match.Type));
            });
    }

    public override void PostConfigureServices(ModuleContext<ModuleLocalizationOption> context)
    {
        _resourceRegistry.ReplaceFromTypes(Option.ResourceMarkerTypes.Concat(_resourceMarkerTypesDiscoveredFromTypeScan));
        Logger.LogInformation("Registered {Count} localization resource marker types.", _resourceRegistry.GetRegistrations().Count);
    }
}

/// <summary>
/// Configures the host's canonical localization profile and explicitly contributed resource markers.
/// </summary>
public class ModuleLocalizationOption : ModuleOptions<ModuleLocalization>
{
    /// <summary>
    /// Default culture when no culture is specified. Default: "zh-CN"
    /// </summary>
    public string DefaultCulture { get; set; } = "zh-CN";

    /// <summary>
    /// List of supported cultures. Default: ["zh-CN", "en-US"]
    /// </summary>
    public List<string> SupportedCultures { get; set; } = ["zh-CN", "en-US"];

    /// <summary>
    /// Culture display names for UI. If not specified, uses CultureInfo.NativeName
    /// </summary>
    public Dictionary<string, string> CultureDisplayNames { get; set; } = new()
    {
        ["zh-CN"] = "简体中文",
        ["en-US"] = "English"
    };

    /// <summary>
    /// Cookie name for culture persistence. Default: ".AspNetCore.Culture"
    /// </summary>
    public string CookieName { get; set; } = ".AspNetCore.Culture";

    internal IReadOnlyList<Type> ResourceMarkerTypes => _resourceMarkerTypes;

    private readonly List<Type> _resourceMarkerTypes = [];

    /// <summary>
    /// Registers a localization resource marker as part of module composition.
    /// </summary>
    /// <typeparam name="TResource">
    /// The marker whose embedded JSON resources belong to the current host. The marker is registered once even when
    /// several modules contribute it.
    /// </typeparam>
    /// <remarks>
    /// Call this from a dependency option contribution in <c>Describe</c> when a module intrinsically consumes a
    /// resource. This keeps direct and transitive module inclusion behavior identical.
    /// </remarks>
    public void AddResource<TResource>()
        where TResource : class, ILocalizationResource
    {
        var resourceType = typeof(TResource);
        if (!_resourceMarkerTypes.Contains(resourceType))
        {
            _resourceMarkerTypes.Add(resourceType);
        }
    }

    internal LocalizationProfile CreateProfile()
    {
        return LocalizationProfile.Create(
            DefaultCulture,
            SupportedCultures,
            CultureDisplayNames,
            CookieName);
    }
}
