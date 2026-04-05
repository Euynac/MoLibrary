using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleCorsBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the CORS (Cross-Origin Resource Sharing) module
        /// </summary>
        public static ModuleCorsGuide AddCors()
        {
            return new ModuleCorsGuide().Register().ConfigCorsMiddleware();
        }
    }
}

[ModuleKey(BuiltInModuleKey.Cors)]
public class ModuleCors(ModuleCorsOption option) : ModuleBase<ModuleCors, ModuleCorsOption, ModuleCorsGuide>(option)
{
}

public class ModuleCorsGuide : ModuleGuide<ModuleCors, ModuleCorsOption, ModuleCorsGuide>
{
    /// <summary>
    /// Configure a permissive CORS policy (intended for development/testing).
    /// <para>Allows any origin, method, and header while supporting credentials (cookies/authorization headers).</para>
    /// <para>Uses <c>SetIsOriginAllowed(_ => true)</c> instead of <c>AllowAnyOrigin()</c> to enable dynamic
    /// Access-Control-Allow-Origin headers required by SignalR and similar scenarios.</para>
    /// <para>Note: restrict allowed origins explicitly in production environments.</para>
    /// </summary>
    public ModuleCorsGuide AllowAll()
    {
        ConfigureServices(context =>
        {
            context.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .SetIsOriginAllowed(_ => true)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });
        });
        return this;
    }

    internal ModuleCorsGuide ConfigCorsMiddleware()
    {
        // Must be registered after UseRouting but before UseAuthorization so CORS takes effect when Origin headers are present
        ConfigureApplicationBuilder(o =>
        {
            o.ApplicationBuilder.UseCors();
        }, ModuleOrder.MIDDLEWARE_USE_ROUTING + 1);

        return this;
    }
}

public class ModuleCorsOption : ModuleOptions<ModuleCors>;
