using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Localization;
using Monica.Core.Localization.Localizers;
using Monica.Core.Localization.Models;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLocalizationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure Localization module
        /// </summary>
        public static ModuleLocalizationGuide AddLocalization(Action<ModuleLocalizationOption>? action = null)
        {
            return new ModuleLocalizationGuide().Register(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Localization)]
public class ModuleLocalization(ModuleLocalizationOption option)
    : WebModuleBase<ModuleLocalization, ModuleLocalizationOption, ModuleLocalizationGuide>(option), IBusinessTypeIterator
{
    private readonly LocalizationResourceRegistry _resourceRegistry = new();
    private readonly List<Type> _discoveredResourceMarkerTypes = [];

    public override bool CanDowngradeToNonWebModule()
    {
        return true;
    }


    public override void ConfigureServices(IServiceCollection services)
    {
        // Add ASP.NET Core localization services
        services.AddLocalization();
        services.TryAddSingleton(_resourceRegistry);

        // Replace default factory with custom JSON-based factory
        services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, MoStringLocalizerFactory>());

        // Configure RequestLocalizationOptions
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = Option.SupportedCultures
                .Select(c => new CultureInfo(c))
                .ToArray();

            options.DefaultRequestCulture = new RequestCulture(Option.DefaultCulture);
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            // Cookie provider for culture persistence
            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider
                {
                    CookieName = Option.CookieName
                }
            ];
        });
    }

    /// <summary>
    /// Collects localization resource marker types discovered through the global type finder.
    /// This path is intended for resource types that live in host or business assemblies participating in the configured scan.
    /// Built-in Monica modules must not rely on this hook because their assemblies may not be part of that scan.
    /// Monica modules should declare a dependency on <see cref="ModuleLocalizationGuide"/> and register resource types through <see cref="ModuleLocalizationGuide.AddResource{TResource}"/>.
    /// </summary>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false } &&
                typeof(IMoLocalizationResource).IsAssignableFrom(type))
            {
                _discoveredResourceMarkerTypes.Add(type);
            }

            yield return type;
        }
    }

    public override void PostConfigureServices(IServiceCollection _)
    {
        _resourceRegistry.ReplaceFromTypes(Option.ResourceMarkerTypes.Concat(_discoveredResourceMarkerTypes));
        Logger.LogInformation("Registered {Count} localization resource marker types.", _resourceRegistry.GetRegistrations().Count);
    }
}

public class ModuleLocalizationGuide : WebModuleGuide<ModuleLocalization, ModuleLocalizationOption, ModuleLocalizationGuide>
{
    public ModuleLocalizationGuide()
    {
        // Register middleware (before routing)
        ConfigureApplicationBuilder(ctx =>
        {
            ctx.ApplicationBuilder.UseRequestLocalization();
        }, ModuleApplicationMiddlewareOrder.BeforeUseRouting);
    }

    /// <summary>
    /// Manually registers a localization resource marker type for Monica modules and other reusable libraries.
    /// Use this method when a resource type should not depend on the host application's business-type scan.
    /// Host or business application resource types should continue to rely on automatic discovery through <see cref="IBusinessTypeIterator"/>.
    /// </summary>
    public ModuleLocalizationGuide AddResource<TResource>() where TResource : class, IMoLocalizationResource
    {
        ConfigureModuleOption(option =>
        {
            var resourceType = typeof(TResource);
            if (!option.ResourceMarkerTypes.Contains(resourceType))
            {
                option.ResourceMarkerTypes.Add(resourceType);
            }
        }, secondKey: typeof(TResource).FullName);

        return this;
    }
}

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

    /// <summary>
    /// Manually registered localization resource marker types.
    /// Built-in Monica modules should add their resource types through <see cref="ModuleLocalizationGuide.AddResource{TResource}"/>
    /// instead of relying on <see cref="IBusinessTypeIterator"/>, which is intended for the host application's scanned types.
    /// </summary>
    public List<Type> ResourceMarkerTypes { get; set; } = [];
}
