using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.ExceptionHandling.Services;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Tool.Extensions;
using Monica.Core.Results;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleExceptionHandlingBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the exception handling module.
        /// </summary>
        public ModuleExceptionHandlingGuide AddExceptionHandling(Action<ModuleExceptionHandlingOption>? action = null)
        {
            return builder.AddModule<ModuleExceptionHandling, ModuleExceptionHandlingOption, ModuleExceptionHandlingGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.ExceptionHandling)]
public class ModuleExceptionHandling(ModuleExceptionHandlingOption option)
    : WebModuleBase<ModuleExceptionHandling, ModuleExceptionHandlingOption, ModuleExceptionHandlingGuide>(option)
{
    /// <summary>
    /// Adds ASP.NET Core exception handling and structured request-rejection responses.
    /// </summary>
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        // Minimal API body binding returns 413 and 415 directly instead of throwing, so translate only those
        // otherwise-empty framework responses into the same result envelope used for thrown binding failures.
        app.UseStatusCodePages(async context =>
        {
            var response = context.HttpContext.Response;
            var rejection = response.StatusCode switch
            {
                StatusCodes.Status413PayloadTooLarge => Res.Fail(
                    "The request payload is too large.",
                    ResStatus.PayloadTooLarge),
                StatusCodes.Status415UnsupportedMediaType => Res.Fail(
                    "The request content type is not supported.",
                    ResStatus.UnsupportedMediaType),
                _ => null
            };

            if (rejection is not null)
            {
                await response.WriteAsJsonAsync(rejection, context.HttpContext.RequestAborted);
            }
        });
        app.UseExceptionHandler();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddProblemDetails();
        services.AddSingleton<IExceptionHandlerService, ExceptionHandlerService>();
        services.AddTransient<IExceptionResponseMapper, BadHttpRequestExceptionMapper>();
        services.AddExceptionHandler<AspNetCoreExceptionHandler>();

        // Minimal API binding otherwise writes an empty 400 response outside Development before endpoint code runs.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
                new BadRequestObjectResult(
                    Res.Fail("Request validation failed.", ResStatus.ValidateError)
                        .AppendMetadata("error", new SerializableError(context.ModelState)));
        });
    }
}

public class ModuleExceptionHandlingGuide
    : WebModuleGuide<ModuleExceptionHandling, ModuleExceptionHandlingOption, ModuleExceptionHandlingGuide>
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

public class ModuleExceptionHandlingOption : ModuleOptions<ModuleExceptionHandling>
{
    /// <summary>
    /// Gets or sets whether unhandled-exception responses include request snapshots, stack traces, and technical details.
    /// The default is <see langword="false"/>. Enable this only for trusted development environments because the
    /// diagnostic payload can contain sensitive application and request data.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; }
}
