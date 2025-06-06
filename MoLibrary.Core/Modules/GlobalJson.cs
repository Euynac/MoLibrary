using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;


public static class ModuleGlobalJsonBuilderExtensions
{
    public static ModuleGlobalJsonGuide ConfigModuleGlobalJson(this WebApplicationBuilder builder,
        Action<ModuleGlobalJsonOption>? action = null)
    {
        return new ModuleGlobalJsonGuide().Register(action);
    }
}

public class ModuleGlobalJson(ModuleGlobalJsonOption option)
    : MoModule<ModuleGlobalJson, ModuleGlobalJsonOption, ModuleGlobalJsonGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.GlobalJson;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        var jsonSerializerOptions = new JsonSerializerOptions();
        jsonSerializerOptions.ConfigGlobalJsonSerializeOptions(Option);
        Option.ExtendAction?.Invoke(jsonSerializerOptions);
        DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions = jsonSerializerOptions;

        //依赖于AsyncLocal技术，异步static单例，不同的请求线程会有不同的HttpContext
        services.AddHttpContextAccessor();

        //巨坑：minimal api 等全局注册
        services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.CloneFrom(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
        });
        //MVC框架HTTP请求与响应的JsonConverter全局注册
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
        {
            o.JsonSerializerOptions.CloneFrom(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
        });

        services.AddSingleton<IGlobalJsonOption, DefaultMoGlobalJsonOptions>();
    }
}

public class ModuleGlobalJsonGuide : MoModuleGuide<ModuleGlobalJson, ModuleGlobalJsonOption, ModuleGlobalJsonGuide>
{


}
