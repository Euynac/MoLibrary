using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Localization.Localizers;
using Monica.Tool.Extensions;

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

public class ModuleLocalization(ModuleLocalizationOption option)
    : MoModule<ModuleLocalization, ModuleLocalizationOption, ModuleLocalizationGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Localization;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Add ASP.NET Core localization services
        services.AddLocalization();

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
}

public class ModuleLocalizationGuide : MoModuleGuide<ModuleLocalization, ModuleLocalizationOption, ModuleLocalizationGuide>
{
    public ModuleLocalizationGuide()
    {
        // Register middleware (before routing)
        ConfigureApplicationBuilder(ctx =>
        {
            ctx.ApplicationBuilder.UseRequestLocalization();
        }, EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);
    }
}

public class ModuleLocalizationOption : MoModuleOption<ModuleLocalization>
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
}

