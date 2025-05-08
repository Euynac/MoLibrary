using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Authority.Security;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.SignalR.Interfaces;
using MoLibrary.Tool.MoResponse;
using SignalRSwaggerGen;

namespace MoLibrary.SignalR.Modules;


public static class ModuleSignalRBuilderExtensions
{
    public static ModuleSignalRGuide AddMoModuleSignalR(this IServiceCollection services,
        Action<ModuleSignalROption>? action = null)
    {
        return new ModuleSignalRGuide().Register(action);
    }
}

public class ModuleSignalR(ModuleSignalROption option) : MoModule<ModuleSignalR, ModuleSignalROption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SignalR;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
}

public class ModuleSignalRGuide : MoModuleGuide<ModuleSignalR, ModuleSignalROption, ModuleSignalRGuide>
{

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(AddMoSignalR)];
    }

    /// <summary>
    ///     注册SignalR
    /// </summary>
    public ModuleSignalRGuide AddMoSignalR<TIHubOperator, THubOperator, TIContract, TIUser>()
        where THubOperator : class, IMoHubOperator<TIContract, TIUser>, TIHubOperator
        where TIHubOperator : class, IMoHubOperator<TIContract, TIUser>
        where TIContract : IMoHubContract
        where TIUser : IMoCurrentUser
    {
        ConfigureExtraServices(nameof(AddMoSignalR), context =>
        {
            context.Services.AddSingleton<IUserIdProvider, MoUserIdProvider>();
            context.Services.AddSingleton<IMoSignalRConnectionManager, MoSignalRConnectionManager>();
            context.Services.AddTransient<IMoHubOperator<TIContract, TIUser>, THubOperator>();
            context.Services.AddTransient<TIHubOperator, THubOperator>();
            context.Services.AddSignalR(options => { options.EnableDetailedErrors = true; }).AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.CloneFrom(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions); //TODO 可配置
            });
        });
        return this;
    }

    /// <summary>
    ///     配置SignalR Swagger显示
    /// </summary>
    public ModuleSignalRGuide AddMoSignalRSwagger(Action<SignalRSwaggerGenOptions> signalROption)
    {
        ConfigureExtraServices(nameof(AddMoSignalRSwagger), context =>
        {
            context.Services.ConfigureSwaggerGen(o =>
            {
                o.AddSignalRSwaggerGen(signalROption);
            });
        });
        return this;
    }

}
