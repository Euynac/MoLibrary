using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.UI.Localization;
using Monica.UI.UISystemInfo.Components;
using Xunit;

namespace Test.Monica.UI.UISystemInfo.Components;

public sealed class SystemInfoServiceHeroTests
{
    [Theory]
    [InlineData("en-US", "Application startup", "Tracked", "Host ready at")]
    [InlineData("zh-CN", "应用启动", "已记录", "宿主就绪于")]
    public async Task Render_WhenStartupIsTracked_ShouldUseBilingualResourcesAndExactF4Duration(
        string cultureName,
        string expectedTitle,
        string expectedBadge,
        string expectedReadyLabel)
    {
        using var culture = new SystemInfoCultureScope(cultureName);
        using var localizationHost = CreateLocalizationHost();
        var localizer = localizationHost.Services.GetRequiredService<IStringLocalizer<SystemInfoResource>>();
        await using var context = new SystemInfoUiTestContext(localizer);
        var snapshot = SystemInfoTestData.Snapshot(startupDurationMs: 3_189.0308);

        var cut = context.Render<SystemInfoServiceHero>(parameters => parameters
            .Add(component => component.Snapshot, snapshot)
            .Add(component => component.NowUtc, snapshot.CapturedAtUtc));

        cut.FindAll(".system-info-readiness").Should().ContainSingle();
        cut.Markup.Should().Contain(expectedTitle);
        cut.Markup.Should().Contain(expectedBadge);
        cut.Markup.Should().Contain(expectedReadyLabel);
        cut.Markup.Should().Contain("3189.0308 ms");
    }

    [Theory]
    [InlineData("en-US", "Application startup")]
    [InlineData("zh-CN", "应用启动")]
    public async Task Render_WhenStartupIsUntracked_ShouldOmitEveryStartupFragment(
        string cultureName,
        string hiddenTitle)
    {
        using var culture = new SystemInfoCultureScope(cultureName);
        using var localizationHost = CreateLocalizationHost();
        var localizer = localizationHost.Services.GetRequiredService<IStringLocalizer<SystemInfoResource>>();
        await using var context = new SystemInfoUiTestContext(localizer);
        var snapshot = SystemInfoTestData.Snapshot();

        var cut = context.Render<SystemInfoServiceHero>(parameters => parameters
            .Add(component => component.Snapshot, snapshot)
            .Add(component => component.NowUtc, snapshot.CapturedAtUtc));

        cut.FindAll(".system-info-readiness").Should().BeEmpty();
        cut.Markup.Should().NotContain(hiddenTitle);
        cut.Markup.Should().NotContain("3189.0308 ms");
    }

    private static IHost CreateLocalizationHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddLocalization().AddResource<SystemInfoResource>();
        });
        return builder.Build();
    }
}
