using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Logging;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Logging.Providers.Serilog;
using Monica.Logging.Services;
using Monica.Tool.General;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleLoggingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Logging 模块
        /// </summary>
        public static ModuleLoggingGuide AddLogging(Action<ModuleLoggingOption>? action = null)
        {
            return new ModuleLoggingGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Logging)]
public class ModuleLogging(ModuleLoggingOption option) : MoModule<ModuleLogging, ModuleLoggingOption, ModuleLoggingGuide>(option)
{
    public override void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        Log.Logger = SerilogLoggingBootstrapper.CreateLogger(builder.Configuration, option);

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: true);

        LogManager.UseFactory(new SerilogLoggerFactory(Log.Logger));
        MoModuleRegisterCentre.Logger = LogManager.For(typeof(MoModuleRegisterCentre));

        var level =
            builder.Configuration.GetSectionRecursively("Serilog:MinimumLevel").Select(p => new { p.Key, p.Value }).ToList().ToJsonString();
        Log.Logger.Information("Set logging level: {LoggingLevel}", level);
    }
}

public class ModuleLoggingGuide : MoModuleGuide<ModuleLogging, ModuleLoggingOption, ModuleLoggingGuide>
{
    public ModuleLoggingGuide AddRequestResponseLoggingMiddleware(bool disableResponse = false, bool disableRequest = false)
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient<HttpRequestLoggingMiddleware>();
            context.Services.AddTransient<HttpResponseLoggingMiddleware>();
        });
        ConfigureApplicationBuilder(context =>
        {
            if (!disableRequest)
            {
                context.ApplicationBuilder.UseMiddleware<HttpRequestLoggingMiddleware>();
            }

            if (!disableResponse)
            {
                context.ApplicationBuilder.UseMiddleware<HttpResponseLoggingMiddleware>();
            }

        }, EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);
        return this;
    }
}

public class ModuleLoggingOption : MoModuleOption<ModuleLogging>
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
