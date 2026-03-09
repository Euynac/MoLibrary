using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleCorsBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 CORS (跨域资源共享) 模块
        /// </summary>
        public static ModuleCorsGuide AddCors()
        {
            return new ModuleCorsGuide().Register().ConfigCorsMiddleware();
        }
    }
}

public class ModuleCors(ModuleCorsOption option) : MoModule<ModuleCors, ModuleCorsOption, ModuleCorsGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Cors;
    }
}

public class ModuleCorsGuide : MoModuleGuide<ModuleCors, ModuleCorsOption, ModuleCorsGuide>
{
    /// <summary>
    /// 配置允许所有来源的 CORS 策略（开发/测试环境使用）。
    /// <para>允许任意来源、方法、请求头，并支持凭据（cookies/authorization headers）。</para>
    /// <para>内部使用 <c>SetIsOriginAllowed(_ => true)</c> 代替 <c>AllowAnyOrigin()</c>，
    /// 以支持 SignalR 等需要动态填写 Access-Control-Allow-Origin 的场景。</para>
    /// <para>注意：生产环境应配置具体的允许来源列表。</para>
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
        //必须在UseRouting之后但在UseAuthorization之前。当请求带Origin Header时生效
        ConfigureApplicationBuilder(o =>
        {
            o.ApplicationBuilder.UseCors();
        }, ModuleOrder.MIDDLEWARE_USE_ROUTING + 1);

        return this;
    }
}

public class ModuleCorsOption : MoModuleOption<ModuleCors>;
