using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features.MoLogProvider;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Logging.Middlewares;
using MoLibrary.Logging.ProviderSerilog;
using MoLibrary.Logging.ProviderSerilog.Enrichers;
using MoLibrary.Tool.General;
using MoLibrary.Tool.MoResponse;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;

namespace MoLibrary.Logging.Modules;


public static class ModuleLoggingBuilderExtensions
{
    public static ModuleLoggingGuide ConfigModuleLogging(this WebApplicationBuilder builder,
        Action<ModuleLoggingOption>? action = null)
    {
        return new ModuleLoggingGuide().Register(action);
    }
}

public class ModuleLogging(ModuleLoggingOption option) : MoModule<ModuleLogging, ModuleLoggingOption, ModuleLoggingGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Logging;
    }

    public override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        var logBuilder = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration);

        Log.Logger = option.CustomLoggerCreator is { } customLoggerCreator
            ? customLoggerCreator.Invoke(logBuilder)
            : CreateLogger(logBuilder);


        builder.Host.UseSerilog(Log.Logger); // 这里只注册了ILogger<T>的泛型日志，所以在依赖注入中使用需要使用泛型。
        GlobalLog.Logger = new SerilogLoggerFactory(Log.Logger).CreateLogger("Global");
        LogProvider.Provider = new SerilogProvider(Log.Logger);
        MoModuleRegisterCentre.Logger = LogProvider.For(typeof(MoModuleRegisterCentre));

        var level =
            builder.Configuration.GetSectionRecursively("Serilog:MinimumLevel").Select(p => new { p.Key, p.Value }).ToList().ToJsonString();
        Log.Logger.Information("Set logging level: {LoggingLevel}", level);
    }

    protected Logger CreateLogger(LoggerConfiguration configuration)
    {
        var path = option.LogFilePath ?? Path.Combine(option.LogFileDirectory ?? AppContext.BaseDirectory, "Logs", option.LogFileName);

        if (!option.DisableEnrichThreadId)
        {
            configuration = configuration.Enrich.WithThreadId();
        }

        if (option.EnableEnrichThreadName)
        {
            configuration = configuration.Enrich.WithThreadName();
        }

        if (!option.DisableWriteToConsole)
        {
            configuration = configuration.WriteTo.Async(s => s.Console(outputTemplate: option.SerilogTemplate));
        }

        if (!option.DisableWriteToFile)
        {
            configuration = configuration.WriteTo.Async(s => s.File(path, outputTemplate: option.SerilogTemplate));
        }

        return configuration.CreateLogger();
    }
}

public class ModuleLoggingGuide : MoModuleGuide<ModuleLogging, ModuleLoggingOption, ModuleLoggingGuide>
{
    public ModuleLoggingGuide AddRequestResponseLoggingMiddleware(bool disableResponse = false, bool disableRequest = false)
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient<RequestLoggingMiddleware>();
            context.Services.AddTransient<ResponseLoggingMiddleware>();
        });
        ConfigureApplicationBuilder(context =>
        {
            if (!disableRequest)
            {
                context.ApplicationBuilder.UseMiddleware<RequestLoggingMiddleware>();
            }

            if (!disableResponse)
            {
                context.ApplicationBuilder.UseMiddleware<ResponseLoggingMiddleware>();
            }

            //builder.UseHttpLogging(); //asp.net 8后启用
        }, EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting); 
        return this;
    }
}

public class ModuleLoggingOption : MoModuleOption<ModuleLogging>
{
    

    /// <summary>
    /// 若使用此配置，则构造Logger的默认配置均失效
    /// </summary>
    public Func<LoggerConfiguration, Logger>? CustomLoggerCreator  { get; set; }



    /// <summary>
    /// <a href="https://github.com/serilog/serilog/wiki/Formatting-Output">Serilog模板文档</a>
    /// </summary>
    public string SerilogTemplate { get; set; } = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3} {ThreadName}TID{ThreadId}] {Message:lj}{Exception}{NewLine}";

    public bool DisableWriteToConsole { get; set; }
    public bool DisableWriteToFile { get; set; }

    /// <summary>
    /// 设定Serilog的日志文件路径，包含文件名，设置后则LogFileDirectory和LogFileName设定失效。
    /// </summary>
    public string? LogFilePath { get; set; }
    
    public string? LogFileDirectory { get; set; }
    public string LogFileName { get; set; } = $"{Assembly.GetEntryAssembly()!.GetName().Name}.log";



    public bool EnableEnrichThreadName { get; set; }
    public bool DisableEnrichThreadId { get; set; }
}