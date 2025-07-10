using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MudBlazor.Services;

namespace MoLibrary.UI.Modules;


public static class ModuleUICoreBuilderExtensions
{
    public static ModuleUICoreGuide ConfigModuleUICore(this WebApplicationBuilder builder,
        Action<ModuleUICoreOption>? action = null)
    {
        return new ModuleUICoreGuide().Register(action).AddBasicMiddlewares();
    }
}

public class ModuleUICore(ModuleUICoreOption option)
    : MoModule<ModuleUICore, ModuleUICoreOption, ModuleUICoreGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.UICore;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Add MudBlazor services
        services.AddMudServices();

        // Add services to the container.
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
    }
}

public class ModuleUICoreGuide : MoModuleGuide<ModuleUICore, ModuleUICoreOption, ModuleUICoreGuide>
{

    public ModuleUICoreGuide AddBasicMiddlewares()
    {
        ConfigureApplicationBuilder(builder =>
        {
            var app = builder.ApplicationBuilder;
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.UseStaticFiles();
            //app.MapStaticAssets();  // .NET 9支持

           
        }, EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);
        ConfigureEndpoints(builder =>
        {
            builder.WebApplication.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();
        });
        return this;
    }
}

public class ModuleUICoreOption : MoModuleOption<ModuleUICore>
{
}