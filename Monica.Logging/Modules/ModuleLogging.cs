using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Logging.Providers.Serilog;
using Monica.Logging.Services;
using Monica.Tool.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLoggingBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure Logging module
        /// </summary>
        public ModuleRegistration<ModuleLogging, ModuleLoggingOption> AddLogging(
            Action<ModuleLoggingOption>? action = null)
        {
            return builder.AddModule<ModuleLogging, ModuleLoggingOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleLogging, ModuleLoggingOption> registration)
    {
        public ModuleRegistration<ModuleLogging, ModuleLoggingOption> AddRequestResponseLoggingMiddleware(
            bool disableResponse = false,
            bool disableRequest = false)
        {
            return registration
                .RequireWebHost("Request/response logging middleware must run in an ASP.NET Core request pipeline.")
                .ConfigureServices(context =>
                {
                    context.Services.AddTransient<HttpRequestLoggingMiddleware>();
                    context.Services.AddTransient<HttpResponseLoggingMiddleware>();
                })
                .ConfigureApplicationBuilder(context =>
                {
                    if (!disableRequest)
                    {
                        context.ApplicationBuilder.UseMiddleware<HttpRequestLoggingMiddleware>();
                    }

                    if (!disableResponse)
                    {
                        context.ApplicationBuilder.UseMiddleware<HttpResponseLoggingMiddleware>();
                    }
                }, ModuleWebStage.BeforeRouting);
        }
    }
}

public class ModuleLogging : MonicaModule<ModuleLoggingOption>, IWebModule
{
    public override void ConfigureBuilder(ModuleBuilderContext<ModuleLoggingOption> context)
    {
        var builder = context.HostApplicationBuilder;
        var serilogLogger = SerilogLoggingBootstrapper.CreateLogger(builder.Configuration, Option);

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(serilogLogger, dispose: true);

        UseCompositionLoggerFactory(new SerilogLoggerFactory(serilogLogger, dispose: false));

        var level =
            builder.Configuration.GetSectionRecursively("Serilog:MinimumLevel").Select(p => new { p.Key, p.Value }).ToList().ToJsonString();
        Logger.LogInformation("Set logging level: {LoggingLevel}", level);
    }
}

public class ModuleLoggingOption : ModuleOptions<ModuleLogging>
{
    /// <summary>
    /// Replaces the default Monica Serilog setup with a custom logger factory.
    /// </summary>
    public Func<LoggerConfiguration, Logger>? CustomLoggerFactory { get; set; }

    /// <summary>
    /// <a href="https://github.com/serilog/serilog/wiki/Formatting-Output">Serilog output template documentation</a>
    /// </summary>
    public string OutputTemplate { get; set; } = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3} {ThreadName}TID{ThreadId}] {Message:lj}{Exception}{NewLine}";

    public bool EnableConsoleSink { get; set; } = true;
    public bool EnableFileSink { get; set; } = true;

    /// <summary>
    /// Sets the explicit log file path including file name. When specified, <see cref="LogDirectory"/> and
    /// <see cref="LogFileName"/> are ignored.
    /// </summary>
    public string? LogFilePath { get; set; }

    public string? LogDirectory { get; set; }
    public string LogFileName { get; set; } = $"{Assembly.GetEntryAssembly()!.GetName().Name}.log";

    public bool EnableThreadNameEnricher { get; set; }
    public bool EnableThreadIdEnricher { get; set; } = true;
    public bool EnableTraceIdEnricher { get; set; }
}
