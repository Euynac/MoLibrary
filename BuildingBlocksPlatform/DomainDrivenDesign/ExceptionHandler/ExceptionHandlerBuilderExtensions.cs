using System.Net;
using BuildingBlocksPlatform.SeedWork;
using MoLibrary.Tool.General;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Tool.MoResponse;
using Microsoft.AspNetCore.Mvc.Filters;
using MoLibrary.AutoModel.Exceptions;

namespace BuildingBlocksPlatform.DomainDrivenDesign.ExceptionHandler;

/// <summary>
/// Extension methods for configuring exception handling in the application.
/// </summary>
/// <remarks><see cref="MoMvcExceptionFilter"/></remarks>
public static class ExceptionHandlerBuilderExtensions
{
    /// <summary>
    /// Adds global exception handling middleware to the application. This middleware should be added early in the pipeline to catch any exceptions that occur during processing.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public static void UseMoExceptionHandler(this IApplicationBuilder app)
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
    }

    public static void AddMoExceptionHandler<THandler>(this IServiceCollection services)
        where THandler : class,IExceptionHandler
    {
        services.AddSingleton<IMoExceptionHandler, MoExceptionHandler>();
        services.AddSingleton<IAsyncExceptionFilter, MoMvcExceptionFilter>();
        //services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlerBehavior<,>));
        services.AddExceptionHandler<AutoModelExceptionHandlerForRes>();
        services.AddExceptionHandler<THandler>();
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
            var ex = (Exception)eventArgs.ExceptionObject;
            GlobalLog.LogError("程序异常捕获：{sender} {eventArgs}", sender?.ToJsonStringForce(), eventArgs?.ToJsonStringForce());
        };

        //巨坑：仅会在失败的Task GC后才会触发该异常。
        TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
        {
            GlobalLog.LogError("任务异常捕获：{sender}", sender?.ToJsonStringForce(), eventArgs?.ToJsonStringForce());
        };

        curDomain.ProcessExit += (sender, eventArgs) =>
        {
            GlobalLog.LogError("进程退出：{sender} {eventArgs}", sender?.ToJsonStringForce(), eventArgs?.ToJsonStringForce());
        };
    }


    /// <summary>
    /// Adds global exception handling services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void AddMoExceptionHandler(this IServiceCollection services)
    {
        AddMoExceptionHandler<DefaultGlobalExceptionHandler>(services);
    }
}

/// <summary>
/// Middleware for handling exceptions in MediatR pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class ExceptionHandlerBehavior<TRequest, TResponse>(IMoExceptionHandler handler, IHttpContextAccessor accessor)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IServiceResponse, new()
{
    /// <summary>
    /// Handles the request and catches any exceptions that occur during processing.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next.Invoke();
        }
        catch (Exception e)
        {
            handler.LogException(accessor.HttpContext, e);
            return (await handler.TryHandleAsync(accessor.HttpContext, e, cancellationToken))
                .ToServiceResponse<TResponse>();
        }
    }
}

/// <summary>
/// Global exception handler for handling exceptions in the application.
/// </summary>
public class DefaultGlobalExceptionHandler(IMoExceptionHandler handler) : IExceptionHandler
{
    public virtual async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var res = await handler.TryHandleAsync(httpContext, exception, cancellationToken);
        handler.LogException(httpContext, exception);
        httpContext.Response.StatusCode =
            (int)(((IServiceResponse?)res)?.GetHttpStatusCode() ?? HttpStatusCode.InternalServerError);

        await httpContext.Response.WriteAsJsonAsync(res, cancellationToken);
        return true;
    }
}

