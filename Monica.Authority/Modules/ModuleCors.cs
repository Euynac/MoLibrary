using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
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
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the CORS (Cross-Origin Resource Sharing) module
        /// </summary>
        public ModuleCorsGuide AddCors()
        {
            return builder.AddModule<ModuleCors, ModuleCorsOption, ModuleCorsGuide>();
        }
    }
}

[ModuleKey(BuiltInModuleKey.Cors)]
public class ModuleCors(ModuleCorsOption option) : WebModuleBase<ModuleCors, ModuleCorsOption, ModuleCorsGuide>(option)
{
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseCors();
    }

    protected override int GetConfigureApplicationBuilderOrder()
    {
        return ModuleOrder.MIDDLEWARE_USE_ROUTING + 1;
    }
}

public class ModuleCorsGuide : WebModuleGuide<ModuleCors, ModuleCorsOption, ModuleCorsGuide>
{
    /// <summary>
    /// Configures the default ASP.NET Core CORS policy used by the Monica pipeline.
    /// </summary>
    /// <param name="configure">A callback that defines the complete default policy.</param>
    /// <returns>The current guide.</returns>
    public ModuleCorsGuide ConfigureDefaultPolicy(Action<CorsPolicyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ConfigureServices(context =>
        {
            context.Services.AddCors(options => options.AddDefaultPolicy(configure));
        });

        return this;
    }

    /// <summary>
    /// Configure a permissive CORS policy (intended for development/testing).
    /// <para>Allows any origin, method, and header while supporting credentials (cookies/authorization headers).</para>
    /// <para>Uses <c>SetIsOriginAllowed(_ => true)</c> instead of <c>AllowAnyOrigin()</c> to enable dynamic
    /// Access-Control-Allow-Origin headers required by SignalR and similar scenarios.</para>
    /// <para>Note: restrict allowed origins explicitly in production environments.</para>
    /// </summary>
    public ModuleCorsGuide AllowAll()
    {
        return ConfigureDefaultPolicy(policy =>
            policy
                .SetIsOriginAllowed(_ => true)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
    }
}

public class ModuleCorsOption : ModuleOptions<ModuleCors>;
