using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Utilities.Connectivity.Facades;
using Monica.Utilities.Connectivity.Services;
using Monica.Utilities.Localization;
using Monica.Utilities.Text.Facades;
using Monica.Utilities.Text.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Registers reusable utility services for connectivity diagnostics and text or JSON transformation.
/// </summary>
public class ModuleUtilities : MonicaModule<ModuleUtilitiesOption>
{
    /// <summary>
    /// Declares the localization resource required by the utility services and UI.
    /// </summary>
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleLocalization, ModuleLocalizationOption>(option =>
        {
            if (!option.ResourceMarkerTypes.Contains(typeof(UtilitiesResource)))
            {
                option.ResourceMarkerTypes.Add(typeof(UtilitiesResource));
            }
        });
    }

    /// <summary>
    /// Registers utility services and result-envelope facades.
    /// </summary>
    public override void ConfigureServices(ModuleContext<ModuleUtilitiesOption> context)
    {
        var services = context.Services;
        services.AddScoped<ConnectivityProbeService>();
        services.AddScoped<ConnectivityProbeFacade>();
        services.AddScoped<TextTransformService>();
        services.AddScoped<TextTransformFacade>();
    }
}

/// <summary>
/// Builder extensions for the utilities module.
/// </summary>
public static class ModuleUtilitiesBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the utilities module that provides connectivity and text-processing services.
        /// </summary>
        /// <param name="action">Optional configuration applied to <see cref="ModuleUtilitiesOption" />.</param>
        /// <returns>The host-bound utilities registration.</returns>
        public ModuleRegistration<ModuleUtilities, ModuleUtilitiesOption> AddUtilities(
            Action<ModuleUtilitiesOption>? action = null)
        {
            return builder.AddModule<ModuleUtilities, ModuleUtilitiesOption>(action);
        }
    }
}

/// <summary>
/// Options that control baseline behavior for the utilities module.
/// </summary>
public class ModuleUtilitiesOption : ModuleOptions<ModuleUtilities>
{
    /// <summary>
    /// Default timeout, in milliseconds, applied by the connectivity probe UI when the user has not supplied a value.
    /// Default: <c>3000</c>.
    /// Configure this when the host commonly probes slow cross-network or cross-region targets.
    /// </summary>
    public int DefaultProbeTimeoutMilliseconds { get; set; } = 3000;

    /// <summary>
    /// Upper bound, in milliseconds, that the connectivity service accepts after normalization.
    /// Default: <c>30000</c>.
    /// Increase this only when the environment legitimately requires longer probe windows.
    /// </summary>
    public int MaximumProbeTimeoutMilliseconds { get; set; } = 30000;

    /// <summary>
    /// Default request path used by HTTP or HTTPS probes when the user leaves the path blank.
    /// Default: <c>/</c>.
    /// Change this when the preferred application liveness endpoint is a different route such as <c>/health</c>.
    /// </summary>
    public string DefaultHttpRequestPath { get; set; } = "/";
}
