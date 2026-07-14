using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
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
