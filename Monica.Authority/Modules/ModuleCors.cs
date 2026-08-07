using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleCorsBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the CORS (Cross-Origin Resource Sharing) module
        /// </summary>
        public ModuleRegistration<ModuleCors, ModuleCorsOption> AddCors()
        {
            return builder.AddModule<ModuleCors, ModuleCorsOption>();
        }
    }

    extension(ModuleRegistration<ModuleCors, ModuleCorsOption> registration)
    {
        public ModuleRegistration<ModuleCors, ModuleCorsOption> ConfigureDefaultPolicy(
            Action<CorsPolicyBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return registration.ConfigureServices(context =>
                context.Services.AddCors(options => options.AddDefaultPolicy(configure)));
        }

        public ModuleRegistration<ModuleCors, ModuleCorsOption> AllowAll()
        {
            return registration.ConfigureDefaultPolicy(policy =>
                policy
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        }
    }
}

public class ModuleCors : MonicaModule<ModuleCorsOption>, IWebHostRequiredModule
{
    public override void ConfigureApplicationBuilder(WebModuleContext<ModuleCorsOption> context)
    {
        context.ApplicationBuilder.UseCors();
    }

    protected override ModuleWebStage GetApplicationBuilderStage() => ModuleWebStage.AfterRouting;
}

public class ModuleCorsOption : ModuleOptions<ModuleCors>;
