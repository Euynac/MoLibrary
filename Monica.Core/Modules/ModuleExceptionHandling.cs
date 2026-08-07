using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.ExceptionHandling.Services;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        public ModuleRegistration<ModuleExceptionHandling, ModuleExceptionHandlingOption> AddExceptionHandling(
            Action<ModuleExceptionHandlingOption>? action = null)
        {
            return builder.AddModule<ModuleExceptionHandling, ModuleExceptionHandlingOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleExceptionHandling, ModuleExceptionHandlingOption> registration)
    {
        public ModuleRegistration<ModuleExceptionHandling, ModuleExceptionHandlingOption> AddExceptionMapper<TMapper>()
            where TMapper : class, IExceptionResponseMapper
        {
            return registration.Configure(options => options.AddExceptionMapper<TMapper>());
        }
    }
}

public class ModuleExceptionHandling : MonicaModule<ModuleExceptionHandlingOption>, IWebHostRequiredModule
{
    /// <summary>
    /// Adds ASP.NET Core exception handling and structured request-rejection responses.
    /// </summary>
    public override void ConfigureApplicationBuilder(WebModuleContext<ModuleExceptionHandlingOption> context)
    {
        // Minimal API body binding returns 413 and 415 directly instead of throwing, so translate only those
        // otherwise-empty framework responses into the same result envelope used for thrown binding failures.
        context.ApplicationBuilder.UseStatusCodePages(async statusCodeContext =>
        {
            var response = statusCodeContext.HttpContext.Response;
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
                await response.WriteAsJsonAsync(rejection, statusCodeContext.HttpContext.RequestAborted);
            }
        });
        context.ApplicationBuilder.UseExceptionHandler();
    }

    public override void ConfigureServices(ModuleContext<ModuleExceptionHandlingOption> context)
    {
        var services = context.Services;
        services.AddHttpContextAccessor();
        services.AddProblemDetails();
        services.AddSingleton<IExceptionHandlerService, ExceptionHandlerService>();
        services.AddTransient<IExceptionResponseMapper, BadHttpRequestExceptionMapper>();
        foreach (var mapperType in Option.ExceptionMapperTypes)
        {
            services.AddTransient(typeof(IExceptionResponseMapper), mapperType);
        }

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

public class ModuleExceptionHandlingOption : ModuleOptions<ModuleExceptionHandling>
{
    private readonly HashSet<Type> _exceptionMapperTypes = [];

    internal IReadOnlyCollection<Type> ExceptionMapperTypes => _exceptionMapperTypes;

    /// <summary>
    /// Gets or sets whether unhandled-exception responses include request snapshots, stack traces, and technical details.
    /// The default is <see langword="false"/>. Enable this only for trusted development environments because the
    /// diagnostic payload can contain sensitive application and request data.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; }

    /// <summary>
    /// Adds a response mapper that participates in exception-to-envelope conversion.
    /// </summary>
    public void AddExceptionMapper<TMapper>() where TMapper : class, IExceptionResponseMapper
    {
        _exceptionMapperTypes.Add(typeof(TMapper));
    }
}
