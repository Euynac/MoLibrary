using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Module registration context used while configuring services, builders, and endpoints.
/// </summary>
/// <param name="services">The service collection.</param>
/// <param name="applicationBuilder">The application builder.</param>
/// <param name="hostApplicationBuilder">The host application builder.</param>
/// <param name="registration">The current module registration.</param>
internal sealed class ModuleConfigurationContext(
    IServiceCollection? services,
    IApplicationBuilder? applicationBuilder,
    IHostApplicationBuilder? hostApplicationBuilder,
    ModuleRegistrationState registration)
{
    /// <summary>
    /// The service collection.
    /// </summary>
    internal IServiceCollection? Services { get; } = services;

    /// <summary>
    /// The application builder.
    /// </summary>
    internal IApplicationBuilder? ApplicationBuilder { get; } = applicationBuilder;

    /// <summary>
    /// The host application builder.
    /// </summary>
    internal IHostApplicationBuilder? HostApplicationBuilder { get; } = hostApplicationBuilder;

    /// <summary>
    /// Gets the current module registration.
    /// </summary>
    internal ModuleRegistrationState Registration { get; } = registration;
}

/// <summary>
/// One ordered lifecycle callback in a compiled module registration.
/// </summary>
internal sealed record ModuleConfigurationRequest(
    ModulePhase Phase,
    int Order,
    long Ordinal,
    Action<ModuleConfigurationContext> Configure,
    ModuleWebStage? WebStage = null);
