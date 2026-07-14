using Microsoft.AspNetCore.Builder;

namespace Monica.DataChannel.Abstractions;

/// <summary>
/// Defines dynamic application configuration hooks.
/// Allows components to configure the ASP.NET Core application during registration.
/// Intended for middleware and endpoints that need to extend the ASP.NET Core pipeline dynamically.
/// </summary>
public interface IApplicationBuilderConfigurable
{
    /// <summary>
    /// Applies application-level configuration.
    /// Uses the component instance to configure the application builder.
    /// Note that this method may be invoked multiple times for components of the same type.
    /// </summary>
    /// <param name="app">The application builder instance.</param>
    void ConfigApplicationBuilder(IApplicationBuilder app);

    /// <summary>
    /// Maps component-owned endpoints into the current application host.
    /// </summary>
    /// <param name="app">The application builder for the host that owns the component.</param>
    void ConfigEndpoints(IApplicationBuilder app);
}
