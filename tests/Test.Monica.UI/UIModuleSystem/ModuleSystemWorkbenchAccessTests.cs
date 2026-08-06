using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.UI.UIModuleSystem.Support;
using Xunit;

namespace Test.Monica.UI.UIModuleSystem;

public sealed class ModuleSystemWorkbenchAccessTests
{
    [Fact]
    public async Task Development_allows_the_workbench_without_host_authentication_services()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var access = new ModuleSystemWorkbenchAccess(
            new TestHostEnvironment(Environments.Development),
            Options.Create(new ModuleSystemUIOption()),
            services);

        (await access.IsAuthorizedAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Production_denies_access_unless_enablement_and_a_policy_are_both_present()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var access = new ModuleSystemWorkbenchAccess(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new ModuleSystemUIOption()),
            services);

        (await access.IsAuthorizedAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Production_uses_the_host_policy_for_the_current_circuit()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options => options.AddPolicy(
            "module-diagnostics",
            policy => policy.RequireAuthenticatedUser()));
        services.AddSingleton<AuthenticationStateProvider>(
            new FixedAuthenticationStateProvider(authenticated: true));
        await using var provider = services.BuildServiceProvider();
        var access = new ModuleSystemWorkbenchAccess(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new ModuleSystemUIOption
            {
                EnableOutsideDevelopment = true,
                AuthorizationPolicy = "module-diagnostics"
            }),
            provider);

        (await access.IsAuthorizedAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Production_denies_an_unauthenticated_principal_even_when_the_policy_is_enabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options => options.AddPolicy(
            "module-diagnostics",
            policy => policy.RequireAuthenticatedUser()));
        services.AddSingleton<AuthenticationStateProvider>(
            new FixedAuthenticationStateProvider(authenticated: false));
        await using var provider = services.BuildServiceProvider();
        var access = new ModuleSystemWorkbenchAccess(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new ModuleSystemUIOption
            {
                EnableOutsideDevelopment = true,
                AuthorizationPolicy = "module-diagnostics"
            }),
            provider);

        (await access.IsAuthorizedAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Production_fails_closed_when_the_configured_policy_is_not_registered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton<AuthenticationStateProvider>(
            new FixedAuthenticationStateProvider(authenticated: true));
        await using var provider = services.BuildServiceProvider();
        var access = new ModuleSystemWorkbenchAccess(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new ModuleSystemUIOption
            {
                EnableOutsideDevelopment = true,
                AuthorizationPolicy = "missing-module-diagnostics-policy"
            }),
            provider);

        var isAuthorized = await access.IsAuthorizedAsync();

        isAuthorized.Should().BeFalse();
    }

    [Fact]
    public void Module_option_validation_rejects_outside_development_enablement_without_a_policy()
    {
        var module = new ModuleSystemUI();
        var options = new ModuleSystemUIOption
        {
            EnableOutsideDevelopment = true
        };

        var validate = () => module.ValidateOptions(options, profileName: null);

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(ModuleSystemUIOption.AuthorizationPolicy)}*");
    }
}

internal sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "Test.Monica.UI";
    public string ContentRootPath { get; set; } = "/test";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class FixedAuthenticationStateProvider(bool authenticated) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.Name, "diagnostics-reviewer")], "test")
            : new ClaimsIdentity();
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }
}
