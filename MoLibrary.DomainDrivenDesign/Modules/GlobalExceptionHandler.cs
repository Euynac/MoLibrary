using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DomainDrivenDesign.ExceptionHandler;
using MoLibrary.Tool.General;
using MoLibrary.Tool.MoResponse;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Diagnostics;

namespace MoLibrary.DomainDrivenDesign.Modules;


public static class ModuleGlobalExceptionHandlerBuilderExtensions
{
    public static ModuleGlobalExceptionHandlerGuide AddMoModuleGlobalExceptionHandler(this WebApplicationBuilder builder,
        Action<ModuleGlobalExceptionHandlerOption>? action = null)
    {
        return new ModuleGlobalExceptionHandlerGuide().Register(action);
    }
}

public class ModuleGlobalExceptionHandler(ModuleGlobalExceptionHandlerOption option)
    : MoModule<ModuleGlobalExceptionHandler, ModuleGlobalExceptionHandlerOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.GlobalExceptionHandler;
    }

    /// <summary>
    /// Adds global exception handling middleware to the application. This middleware should be added early in the pipeline to catch any exceptions that occur during processing.
    /// </summary>
    
    //TODO Order 100
    public override Res ConfigureApplicationBuilder(IApplicationBuilder app)
    {

        //if (config.AppOptions.IsDebugging)
        //{
        //    app.UseDeveloperExceptionPage();
        //}
        //else
        //{  
        //    app.UseExceptionHandler(_ => { });
        //}
        app.UseExceptionHandler(_ => { });

        //重写RESTful API异常处理
        //app.UseExceptionHandler(c => c.Run(async context =>
        //{
        //    var feature = context.Features
        //        .Get<IExceptionHandlerPathFeature>();
        //    var exception = feature?.Error;
        //    var response =
        //        new Res(exception ??
        //                new Exception($"未知异常，在{feature?.Path}上。{feature.ToJsonStringForce()}"));
        //    await context.Response.WriteAsJsonAsync(response);
        //}));
        return base.ConfigureApplicationBuilder(app);
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoExceptionHandler, MoExceptionHandler>();
        services.AddSingleton<IAsyncExceptionFilter, MoMvcExceptionFilter>();
        //services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlerBehavior<,>));
      
        //services.AddProblemDetails();
        //services.Configure<MvcOptions>(options =>
        //{
        //    options.Filters.Add(typeof(MoMvcExceptionFilter));
        //});

        services.Configure<ApiBehaviorOptions>(o =>
        {
            //o.InvalidModelStateResponseFactory = actionContext =>
            //    new BadRequestObjectResult(actionContext.ModelState);//这是Default behaviour，该配置仅适用于ApiController标签的ControllerBase类

            o.InvalidModelStateResponseFactory = context =>
                new BadRequestObjectResult(
                    Res.CreateError(new SerializableError(context.ModelState), "接口请求参数校验失败",
                        ResponseCode.ValidateError));
        });

        var curDomain = AppDomain.CurrentDomain;
        curDomain.UnhandledException += (sender, eventArgs) =>
        {
            var ex = (Exception) eventArgs.ExceptionObject;
            Logger.LogError("程序异常捕获：{sender} {eventArgs}", sender?.ToJsonStringForce(), eventArgs?.ToJsonStringForce());

            if (eventArgs is { IsTerminating: true })
            {
                Logger.LogWarning("程序因未处理异常而终止。可能是由于使用了async void方法，此方法引发的异常无法被上层catch，请检查代码。");
            }
        };

        //巨坑：仅会在失败的Task GC后才会触发该异常。
        TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
        {
            Logger.LogError("任务异常捕获：{sender}", sender?.ToJsonStringForce(), eventArgs?.ToJsonStringForce());
        };

        curDomain.ProcessExit += (sender, eventArgs) =>
        {
            Logger.LogError("进程退出：{sender} {eventArgs}", sender?.ToJsonStringForce(), eventArgs?.ToJsonStringForce());
        };
        return Res.Ok();
    }
}

public class ModuleGlobalExceptionHandlerGuide : MoModuleGuide<ModuleGlobalExceptionHandler,
    ModuleGlobalExceptionHandlerOption, ModuleGlobalExceptionHandlerGuide>
{

    public ModuleGlobalExceptionHandlerGuide AddDefaultExceptionHandler()
    {
        ConfigureServices(nameof(AddDefaultExceptionHandler), context =>
        {
            context.Services.AddExceptionHandler<DefaultGlobalExceptionHandler>();
        });
        return this;
    }

    public ModuleGlobalExceptionHandlerGuide AddCustomExceptionHandler<THandler>() where THandler : class, IExceptionHandler
    {
        ConfigureServices($"{nameof(AddCustomExceptionHandler)}_{typeof(THandler).Name}", context =>
        {
            context.Services.AddExceptionHandler<THandler>();
        });
        return this;
    }
}

public class ModuleGlobalExceptionHandlerOption : IMoModuleOption<ModuleGlobalExceptionHandler>
{
}