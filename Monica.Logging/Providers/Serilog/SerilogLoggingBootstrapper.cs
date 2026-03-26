using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Monica.Logging.Extensions;
using Monica.Modules;

namespace Monica.Logging.Providers.Serilog;

internal static class SerilogLoggingBootstrapper
{
    public static Logger CreateLogger(IConfiguration configuration, ModuleLoggingOption option)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration);

        if (option.CustomLoggerFactory is { } customLoggerFactory)
        {
            return customLoggerFactory(loggerConfiguration);
        }

        var configuredLogger = ApplyEnrichers(loggerConfiguration, option);
        configuredLogger = ApplySinks(configuredLogger, option);

        return configuredLogger.CreateLogger();
    }

    private static LoggerConfiguration ApplyEnrichers(LoggerConfiguration configuration, ModuleLoggingOption option)
    {
        if (option.EnableThreadIdEnricher)
        {
            configuration = configuration.Enrich.WithThreadId();
        }

        if (option.EnableThreadNameEnricher)
        {
            configuration = configuration.Enrich.WithThreadName();
        }

        if (option.EnableTraceIdEnricher)
        {
            configuration = configuration.Enrich.WithTraceId();
        }

        return configuration;
    }

    private static LoggerConfiguration ApplySinks(LoggerConfiguration configuration, ModuleLoggingOption option)
    {
        if (option.EnableConsoleSink)
        {
            configuration = configuration.WriteTo.Async(sink =>
                sink.Console(outputTemplate: option.OutputTemplate));
        }

        if (option.EnableFileSink)
        {
            configuration = configuration.WriteTo.Async(sink =>
                sink.File(ResolveLogFilePath(option), outputTemplate: option.OutputTemplate));
        }

        return configuration;
    }

    private static string ResolveLogFilePath(ModuleLoggingOption option)
    {
        return option.LogFilePath
            ?? Path.Combine(option.LogDirectory ?? AppContext.BaseDirectory, "Logs", option.LogFileName);
    }
}
