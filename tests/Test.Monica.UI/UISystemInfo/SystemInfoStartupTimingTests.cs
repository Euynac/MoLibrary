using System.Globalization;
using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Results;
using Monica.Modules;
using Monica.Testing.Localization;
using Monica.UI.Localization;
using Monica.UI.UISystemInfo.Components;
using Monica.UI.UISystemInfo.Models;
using Monica.UI.UISystemInfo.Support;
using MudBlazor.Services;
using Xunit;

namespace Test.Monica.UI.UISystemInfo;

public sealed class SystemInfoStartupTimingTests
{
    [Fact]
    public async Task SystemInfoService_ShouldReturnTheOptInApplicationStartupTiming()
    {
        var builder = Host.CreateApplicationBuilder();
        var startup = MonicaStartup.Start();
        builder.AddMonica(startup, static monica =>
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault()));
        using var host = builder.Build();
        await host.StartAsync(Xunit.TestContext.Current.CancellationToken);

        try
        {
            var application = host.Services.GetRequiredService<MonicaApplication>();
            var service = new SystemInfoService(
                NullLogger<SystemInfoService>.Instance,
                new EchoStringLocalizer<SystemInfoResource>(),
                new ServerAddressesFeature(),
                application,
                host.Services.GetRequiredService<IHostApplicationLifetime>(),
                Options.Create(new ModuleSystemInfoUIOption()));

            var result = await service.GetSystemInfoAsync(simple: true);

            result.Status.Should().Be(ResStatus.Ok);
            result.Data!.ApplicationReadyAtUtc.Should().Be(application.StartupTiming!.ReadyAtUtc);
            result.Data.ApplicationStartupDurationMs.Should().Be(application.StartupTiming.DurationMs);
        }
        finally
        {
            await host.StopAsync(Xunit.TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task SystemInfoService_ShouldOmitApplicationStartupTiming_WhenHostDidNotOptIn()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(static monica =>
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault()));
        using var host = builder.Build();
        await host.StartAsync(Xunit.TestContext.Current.CancellationToken);

        try
        {
            var service = new SystemInfoService(
                NullLogger<SystemInfoService>.Instance,
                new EchoStringLocalizer<SystemInfoResource>(),
                new ServerAddressesFeature(),
                host.Services.GetRequiredService<MonicaApplication>(),
                host.Services.GetRequiredService<IHostApplicationLifetime>(),
                Options.Create(new ModuleSystemInfoUIOption()));

            var result = await service.GetSystemInfoAsync(simple: true);

            result.Status.Should().Be(ResStatus.Ok);
            result.Data!.ApplicationReadyAtUtc.Should().BeNull();
            result.Data.ApplicationStartupDurationMs.Should().BeNull();
        }
        finally
        {
            await host.StopAsync(Xunit.TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("en-US", "Application startup", "Tracked", "Ready at")]
    [InlineData("zh-CN", "应用启动", "已记录", "就绪于")]
    public async Task BasicInfoCard_ShouldRenderTrackedApplicationStartupFromBilingualResources(
        string cultureName,
        string expectedLabel,
        string expectedBadge,
        string expectedReadyLabel)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddLocalization().AddResource<SystemInfoResource>();
        });
        using var localizationHost = builder.Build();
        var localizer = localizationHost.Services
            .GetRequiredService<IStringLocalizer<SystemInfoResource>>();
        using var culture = new CultureScope(cultureName);
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(localizer);
        var now = DateTimeOffset.UtcNow;
        var info = new SystemInfoResponse
        {
            BuildTime = now.LocalDateTime,
            LocalTime = now.LocalDateTime,
            UtcTime = now.UtcDateTime,
            TimeZone = SystemTimeZone.CaptureLocal(now),
            ApplicationReadyAtUtc = now,
            ApplicationStartupDurationMs = 3_189.0308
        };

        var cut = context.Render<SystemInfoBasicInfoCard>(parameters => parameters
            .Add(component => component.Info, info));

        cut.Markup.Should().Contain(expectedLabel);
        cut.Markup.Should().Contain(expectedBadge);
        cut.Markup.Should().Contain(expectedReadyLabel);
        cut.Markup.Should().Contain("3189.0308 ms");
    }

    [Theory]
    [InlineData("en-US", "Application startup")]
    [InlineData("zh-CN", "应用启动")]
    public async Task BasicInfoCard_ShouldHideApplicationStartupSection_WhenTimingWasNotRequested(
        string cultureName,
        string hiddenLabel)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddLocalization().AddResource<SystemInfoResource>();
        });
        using var localizationHost = builder.Build();
        var localizer = localizationHost.Services
            .GetRequiredService<IStringLocalizer<SystemInfoResource>>();
        using var culture = new CultureScope(cultureName);
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(localizer);
        var now = DateTimeOffset.UtcNow;
        var info = new SystemInfoResponse
        {
            BuildTime = now.LocalDateTime,
            LocalTime = now.LocalDateTime,
            UtcTime = now.UtcDateTime,
            TimeZone = SystemTimeZone.CaptureLocal(now)
        };

        var cut = context.Render<SystemInfoBasicInfoCard>(parameters => parameters
            .Add(component => component.Info, info));

        cut.Markup.Should().NotContain(hiddenLabel);
        cut.FindAll(".system-info-basic-card__startup").Should().BeEmpty();
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        internal CultureScope(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
