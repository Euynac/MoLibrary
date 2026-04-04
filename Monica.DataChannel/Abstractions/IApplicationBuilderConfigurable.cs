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
    public void ConfigApplicationBuilder(IApplicationBuilder app);

    public void ConfigEndpoints(IApplicationBuilder app);
}
