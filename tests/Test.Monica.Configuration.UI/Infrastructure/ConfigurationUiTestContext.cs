using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monica.Configuration.Facades;
using Monica.Configuration.UI.Localization;
using Monica.Testing.Localization;
using Monica.Modules;
using MudBlazor;
using MudBlazor.Services;

namespace Test.Monica.Configuration.UI.Infrastructure;

internal sealed class ConfigurationUiTestContext : BunitContext
{
    internal IRenderedComponent<MudDialogProvider> DialogProvider { get; }

    internal ConfigurationUiTestContext(
        ConfigurationFacade? facade = null,
        ModuleConfigurationUIOption? option = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IStringLocalizer<ConfigurationUIResource>, EchoStringLocalizer<ConfigurationUIResource>>();
        Services.AddSingleton<IOptions<ModuleConfigurationUIOption>>(Options.Create(option ?? new ModuleConfigurationUIOption()));
        if (facade is not null)
        {
            Services.AddSingleton(facade);
        }

        _ = Render<MudPopoverProvider>();
        DialogProvider = Render<MudDialogProvider>();
    }
}
