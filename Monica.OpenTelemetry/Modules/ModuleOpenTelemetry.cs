using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.OpenTelemetry.InProcessCollector.Facades;
using Monica.OpenTelemetry.InProcessCollector.Services;
using Monica.OpenTelemetry.Localization;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions used to register Monica's OpenTelemetry SDK wiring module.
/// </summary>
public static class ModuleOpenTelemetryBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers Monica's out-of-the-box OpenTelemetry SDK wiring for metrics exporters and instrumentations.
        /// </summary>
        /// <param name="action">Optional module option configuration delegate.</param>
        /// <returns>The module guide used to continue OpenTelemetry registration.</returns>
        public static ModuleOpenTelemetryGuide AddOpenTelemetry(Action<ModuleOpenTelemetryOption>? action = null)
        {
            return new ModuleOpenTelemetryGuide().Register(action);
        }
    }
}

/// <summary>
/// OpenTelemetry integration module that wires metrics exporters, instrumentations, and optional in-process snapshots.
/// </summary>
/// <param name="option">The module configuration options.</param>
[ModuleKey(BuiltInModuleKey.OpenTelemetry)]
public class ModuleOpenTelemetry(ModuleOpenTelemetryOption option)
    : WebModuleBase<ModuleOpenTelemetry, ModuleOpenTelemetryOption, ModuleOpenTelemetryGuide>(option)
{
    /// <inheritdoc />
    public override bool CanDowngradeToNonWebModule()
    {
        return true;
    }

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<OpenTelemetryResource>();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => ConfigureResource(resource, Option))
            .WithMetrics(metrics => ConfigureMetrics(metrics, Option));

        if (!Option.EnableInProcessCollector)
        {
            return;
        }

        services.TryAddSingleton<InProcessMetricsCollector>();
        services.AddHostedService(sp => sp.GetRequiredService<InProcessMetricsCollector>());
        services.AddScoped<OpenTelemetryFacade>();
    }

    /// <inheritdoc />
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            if (Option.UsePrometheusEndpoint)
            {
                endpoints.MapPrometheusScrapingEndpoint(Option.PrometheusEndpointPath)
                    .WithMetadata(MonicaMinimalApiMetadata.Instance);
            }

            if (!Option.EnableInProcessCollector)
            {
                return;
            }

            endpoints.MapGet(
                    "/opentelemetry/snapshot",
                    async ([FromServices] OpenTelemetryFacade facade) =>
                        (await facade.GetSnapshotAsync()).GetResponse())
                .WithName("Get OpenTelemetry in-process snapshot")
                .WithTags(Option.GetApiGroupName())
                .WithSummary("Returns the in-process metric snapshot collected by Monica.OpenTelemetry.")
                .WithDescription("Returns a per-instance, in-memory snapshot of subscribed meter measurements. Use an exporter for production retention and multi-instance analysis.");
        });
    }

    private static void ConfigureResource(ResourceBuilder resource, ModuleOpenTelemetryOption option)
    {
        resource.AddService(option.GetResourceServiceName(), serviceVersion: option.GetResourceServiceVersion());

        if (!string.IsNullOrWhiteSpace(option.DeploymentEnvironment))
        {
            resource.AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", option.DeploymentEnvironment)
            ]);
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, ModuleOpenTelemetryOption option)
    {
        foreach (var pattern in option.GetSdkMeterPatterns())
        {
            metrics.AddMeter(pattern);
        }

        if (option.IncludeAspNetCoreInstrumentation)
        {
            metrics.AddAspNetCoreInstrumentation();
        }

        if (option.IncludeHttpClientInstrumentation)
        {
            metrics.AddHttpClientInstrumentation();
        }

        if (option.IncludeRuntimeInstrumentation)
        {
            metrics.AddRuntimeInstrumentation();
        }

        if (option.UseOtlpExporter)
        {
            metrics.AddOtlpExporter(exporter =>
            {
                if (!string.IsNullOrWhiteSpace(option.OtlpEndpoint))
                {
                    exporter.Endpoint = new Uri(option.OtlpEndpoint, UriKind.Absolute);
                }

                exporter.Protocol = option.OtlpProtocol;
            });
        }

        if (option.UsePrometheusEndpoint)
        {
            metrics.AddPrometheusExporter();
        }

        if (option.UseConsoleExporter)
        {
            metrics.AddConsoleExporter();
        }
    }
}

/// <summary>
/// Fluent registration guide for the Monica OpenTelemetry integration module.
/// </summary>
public class ModuleOpenTelemetryGuide
    : WebModuleGuide<ModuleOpenTelemetry, ModuleOpenTelemetryOption, ModuleOpenTelemetryGuide>
{
    /// <summary>
    /// Enables the OTLP metrics exporter. Environment variables are honored by the OpenTelemetry SDK when no endpoint is supplied.
    /// </summary>
    /// <param name="endpoint">Optional absolute OTLP endpoint URI.</param>
    /// <param name="protocol">OTLP transport protocol.</param>
    /// <returns>The current guide for fluent chaining.</returns>
    public ModuleOpenTelemetryGuide UseOtlpExporter(string? endpoint = null, OtlpExportProtocol protocol = OtlpExportProtocol.Grpc)
    {
        ConfigureModuleOption(option =>
        {
            option.UseOtlpExporter = true;
            option.OtlpEndpoint = endpoint;
            option.OtlpProtocol = protocol;
        });
        return this;
    }

    /// <summary>
    /// Exposes a Prometheus scraping endpoint for metrics.
    /// </summary>
    /// <param name="path">Endpoint path used by Prometheus scrapers. Defaults to <c>/metrics</c>.</param>
    /// <returns>The current guide for fluent chaining.</returns>
    public ModuleOpenTelemetryGuide UsePrometheusEndpoint(string path = "/metrics")
    {
        ConfigureModuleOption(option =>
        {
            option.UsePrometheusEndpoint = true;
            option.PrometheusEndpointPath = path;
        });
        return this;
    }

    /// <summary>
    /// Adds the console metrics exporter for local debugging.
    /// </summary>
    /// <returns>The current guide for fluent chaining.</returns>
    public ModuleOpenTelemetryGuide UseConsoleExporter()
    {
        ConfigureModuleOption(option => option.UseConsoleExporter = true);
        return this;
    }

    /// <summary>
    /// Enables Monica's bounded in-process metrics collector used by the snapshot endpoint and dashboard UI.
    /// </summary>
    /// <param name="configure">Optional additional collector configuration.</param>
    /// <returns>The current guide for fluent chaining.</returns>
    public ModuleOpenTelemetryGuide UseInProcessCollector(Action<ModuleOpenTelemetryOption>? configure = null)
    {
        ConfigureModuleOption(option =>
        {
            option.EnableInProcessCollector = true;
            configure?.Invoke(option);
        });
        return this;
    }

    /// <summary>
    /// Controls whether Monica's in-process collector subscribes to meters implied by enabled built-in SDK instrumentations.
    /// </summary>
    /// <param name="enabled">
    /// <see langword="true"/> to include the ASP.NET Core, HttpClient, and runtime meters selected by the instrumentation flags;
    /// <see langword="false"/> to collect only <see cref="ModuleOpenTelemetryOption.MeterPatterns"/>.
    /// </param>
    /// <returns>The current guide for fluent chaining.</returns>
    public ModuleOpenTelemetryGuide UseInstrumentationMetersInProcessCollector(bool enabled = true)
    {
        ConfigureModuleOption(option => option.IncludeInstrumentationMetersInProcessCollector = enabled);
        return this;
    }

    /// <summary>
    /// Adds another meter name or wildcard pattern to the OpenTelemetry subscription list.
    /// </summary>
    /// <param name="pattern">Meter name or wildcard pattern such as <c>System.*</c>.</param>
    /// <returns>The current guide for fluent chaining.</returns>
    public ModuleOpenTelemetryGuide AddMeter(string pattern)
    {
        ConfigureModuleOption(option =>
        {
            if (!option.MeterPatterns.Contains(pattern, StringComparer.Ordinal))
            {
                option.MeterPatterns.Add(pattern);
            }
        }, secondKey: pattern);
        return this;
    }
}

/// <summary>
/// Configuration options for Monica's OpenTelemetry SDK wiring.
/// </summary>
public class ModuleOpenTelemetryOption : MinimalApiModuleOptions<ModuleOpenTelemetry>
{
    private static readonly string[] AspNetCoreInstrumentationMeterPatterns = ["Microsoft.AspNetCore.*"];
    private static readonly string[] HttpClientInstrumentationMeterPatterns = ["System.Net.Http", "System.Net.NameResolution"];
    private static readonly string[] RuntimeInstrumentationMeterPatterns = ["System.Runtime", "OpenTelemetry.Instrumentation.Runtime"];

    /// <summary>
    /// Gets or sets the OpenTelemetry resource service name.
    /// When not configured, the module uses the application defaults configured through <see cref="Mo.ConfigApplication"/>
    /// and resolves to the project name.
    /// </summary>
    public string? ResourceServiceName { get; set; }

    /// <summary>
    /// Gets or sets the optional OpenTelemetry resource service version.
    /// When not configured, the module uses the application version configured through <see cref="Mo.ConfigApplication"/>.
    /// </summary>
    public string? ResourceServiceVersion { get; set; }

    /// <summary>
    /// Resolves the OpenTelemetry resource service name.
    /// </summary>
    public string GetResourceServiceName() => Mo.Application.ResolveProjectName(ResourceServiceName);

    /// <summary>
    /// Resolves the OpenTelemetry resource service version.
    /// </summary>
    public string? GetResourceServiceVersion() => Mo.Application.ResolveAppVersion(ResourceServiceVersion);

    /// <summary>
    /// Gets or sets the optional deployment environment resource attribute, such as <c>Development</c> or <c>Production</c>.
    /// </summary>
    public string? DeploymentEnvironment { get; set; }

    /// <summary>
    /// Gets custom meter names or wildcard patterns subscribed by the OpenTelemetry SDK and Monica's in-process collector.
    /// Defaults to <c>Monica.*</c>, which captures every first-party Monica meter. Built-in instrumentation meters are
    /// added to the in-process collector separately when <see cref="IncludeInstrumentationMetersInProcessCollector"/> is enabled.
    /// </summary>
    public List<string> MeterPatterns { get; set; } = ["Monica.*"];

    /// <summary>
    /// Gets or sets whether ASP.NET Core server metrics instrumentation is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeAspNetCoreInstrumentation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether outbound <see cref="HttpClient"/> metrics instrumentation is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeHttpClientInstrumentation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether runtime metrics instrumentation is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeRuntimeInstrumentation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the in-process collector should also subscribe to the built-in meters implied by enabled SDK
    /// instrumentation flags. Defaults to <see langword="true"/>, so the dashboard shows ASP.NET Core, HttpClient,
    /// and runtime metrics when those instrumentations are enabled. Set to <see langword="false"/> when the dashboard
    /// should only display meters listed in <see cref="MeterPatterns"/>.
    /// </summary>
    public bool IncludeInstrumentationMetersInProcessCollector { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the OTLP metrics exporter is enabled.
    /// The OpenTelemetry SDK honors <c>OTEL_EXPORTER_OTLP_*</c> environment variables unless an option overrides them.
    /// </summary>
    public bool UseOtlpExporter { get; set; }

    /// <summary>
    /// Gets or sets an optional absolute OTLP endpoint URI that overrides SDK environment configuration.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the OTLP transport protocol. Defaults to gRPC.
    /// </summary>
    public OtlpExportProtocol OtlpProtocol { get; set; } = OtlpExportProtocol.Grpc;

    /// <summary>
    /// Gets or sets whether to expose a Prometheus scraping endpoint. Defaults to <see langword="false"/>.
    /// </summary>
    public bool UsePrometheusEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the Prometheus scraping endpoint path. Defaults to <c>/metrics</c>.
    /// </summary>
    public string PrometheusEndpointPath { get; set; } = "/metrics";

    /// <summary>
    /// Gets or sets whether to enable the console metrics exporter for local debugging.
    /// </summary>
    public bool UseConsoleExporter { get; set; }

    /// <summary>
    /// Gets or sets whether to enable the bounded in-process collector used by snapshot APIs and the dashboard UI.
    /// </summary>
    public bool EnableInProcessCollector { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of samples retained for each instrument tag set.
    /// Higher values improve local charts but increase per-process memory usage.
    /// </summary>
    public int SamplesPerSeries { get; set; } = 600;

    /// <summary>
    /// Gets or sets the maximum number of distinct tag sets retained per instrument.
    /// When exceeded, the collector reports overflow and drops additional tag sets for that instrument.
    /// </summary>
    public int MaxTagSetsPerInstrument { get; set; } = 200;

    /// <summary>
    /// Gets or sets how often observable instruments are sampled by the in-process collector.
    /// </summary>
    public TimeSpan ObservableInstrumentInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the normalized custom meter patterns supplied to the OpenTelemetry SDK through <c>AddMeter</c>.
    /// Built-in instrumentation meters are activated through their dedicated SDK instrumentation calls.
    /// </summary>
    /// <returns>The distinct, non-empty SDK meter patterns in configured order.</returns>
    public IReadOnlyList<string> GetSdkMeterPatterns()
    {
        return NormalizeMeterPatterns(MeterPatterns);
    }

    /// <summary>
    /// Gets the normalized meter patterns used by Monica's in-process collector.
    /// </summary>
    /// <returns>
    /// The custom meter patterns plus built-in instrumentation meters selected by the enabled instrumentation flags.
    /// An empty list intentionally means the collector accepts every published meter.
    /// </returns>
    public IReadOnlyList<string> GetInProcessCollectorMeterPatterns()
    {
        var patterns = NormalizeMeterPatterns(MeterPatterns);
        if (patterns.Count == 0 || !IncludeInstrumentationMetersInProcessCollector)
        {
            return patterns;
        }

        if (IncludeAspNetCoreInstrumentation)
        {
            AddMeterPatterns(patterns, AspNetCoreInstrumentationMeterPatterns);
        }

        if (IncludeHttpClientInstrumentation)
        {
            AddMeterPatterns(patterns, HttpClientInstrumentationMeterPatterns);
        }

        if (IncludeRuntimeInstrumentation)
        {
            AddMeterPatterns(patterns, RuntimeInstrumentationMeterPatterns);
        }

        return patterns;
    }

    private static List<string> NormalizeMeterPatterns(IEnumerable<string> patterns)
    {
        return patterns
            .Where(static pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(static pattern => pattern.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void AddMeterPatterns(List<string> target, IReadOnlyList<string> source)
    {
        foreach (var pattern in source)
        {
            if (!target.Contains(pattern, StringComparer.Ordinal))
            {
                target.Add(pattern);
            }
        }
    }
}
