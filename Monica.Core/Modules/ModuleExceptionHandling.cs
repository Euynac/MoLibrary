using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.ExceptionHandling;
using Monica.Core.ExceptionHandling.Interfaces;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Tool.Extensions;
using Monica.Tool.General;
using Monica.Tool.MoResponse;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleExceptionHandlingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the exception handling module.
        /// </summary>
        public static ModuleExceptionHandlingGuide AddExceptionHandling(Action<ModuleExceptionHandlingOption>? action = null)
        {
            return new ModuleExceptionHandlingGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.ExceptionHandling)]
public class ModuleExceptionHandling(ModuleExceptionHandlingOption option)
    : MoModule<ModuleExceptionHandling, ModuleExceptionHandlingOption, ModuleExceptionHandlingGuide>(option)
{
    /// <summary>
    /// Adds the ASP.NET Core exception handling middleware.
    /// </summary>
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseExceptionHandler();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddProblemDetails();
        services.AddSingleton<IExceptionHandlerService, ExceptionHandlerService>();
        services.AddExceptionHandler<AspNetCoreExceptionHandler>();

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
                new BadRequestObjectResult(
                    Res.CreateError(
                        new SerializableError(context.ModelState),
                        "接口请求参数校验失败",
                        ResponseCode.ValidateError));
        });

        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.UnhandledException += (sender, eventArgs) =>
        {
            Logger.LogError(
                "Unhandled application exception captured: {Sender} {EventArgs}",
                sender?.ToJsonStringForce(),
                eventArgs?.ToJsonStringForce());

            if (eventArgs is { IsTerminating: true })
            {
                Logger.LogWarning(
                    "The process is terminating because of an unhandled exception. Check for async void usage or other unobserved failures.");
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
        {
            Logger.LogError(
                "Unobserved task exception captured: {Sender} {EventArgs}",
                sender?.ToJsonStringForce(),
                eventArgs?.ToJsonStringForce());
        };

        currentDomain.ProcessExit += (sender, eventArgs) =>
        {
            Logger.LogError(
                "Process exit captured: {Sender} {EventArgs}",
                sender?.ToJsonStringForce(),
                eventArgs?.ToJsonStringForce());
        };
    }
}

public class ModuleExceptionHandlingGuide
    : MoModuleGuide<ModuleExceptionHandling, ModuleExceptionHandlingOption, ModuleExceptionHandlingGuide>
{
    public ModuleExceptionHandlingGuide AddExceptionMapper<TMapper>() where TMapper : class, IExceptionResponseMapper
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient<IExceptionResponseMapper, TMapper>();
        }, secondKey: typeof(TMapper).GetCleanFullName());
        return this;
    }
}

public class ModuleExceptionHandlingOption : MoModuleOption<ModuleExceptionHandling>
{
}
