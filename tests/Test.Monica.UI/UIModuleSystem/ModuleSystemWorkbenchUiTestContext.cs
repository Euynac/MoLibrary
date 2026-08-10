using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Monica.Testing.Localization;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.Shell.Support;
using Monica.UI.UIModuleSystem.Support;
using MudBlazor;
using MudBlazor.Services;

namespace Test.Monica.UI.UIModuleSystem;

internal sealed class ModuleSystemWorkbenchUiTestContext : BunitContext
{
    internal ModuleSystemWorkbenchUiTestContext(
        IStringLocalizer<ModuleSystemResource>? moduleSystemLocalizer = null,
        Action<IServiceCollection>? configureServices = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IStringLocalizer<ModuleSystemResource>>(
            moduleSystemLocalizer ?? new EchoStringLocalizer<ModuleSystemResource>());
        configureServices?.Invoke(Services);

        var pages = new PageRegistry();
        pages.RegisterPage<ModuleSystemPage>(
            ModuleSystemPage.MODULE_SYSTEM_DASHBOARD_URL,
            "Module system",
            accessPolicyType: typeof(ModuleSystemWorkbenchAccess));
        pages.Seal();
        Services.AddSingleton<IPageCatalog>(pages);
        Services.AddScoped<PageAccessEvaluator>();

        _ = Render<MudPopoverProvider>();
    }
}
