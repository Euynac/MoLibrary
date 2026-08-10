using AwesomeAssertions;
using Bunit;
using Monica.UI.UISystemInfo.Components;
using Xunit;

namespace Test.Monica.UI.UISystemInfo.Components;

public sealed class SystemInfoPulseStripTests
{
    [Fact]
    public async Task PulseCards_ShouldUseTheSharedThemeAwareSurfaceAndRailContract()
    {
        await using var context = new SystemInfoUiTestContext();
        var snapshot = SystemInfoTestData.Snapshot();
        var cut = context.Render<SystemInfoPulseStrip>(parameters => parameters
            .Add(component => component.Snapshot, snapshot)
            .Add(component => component.NowUtc, snapshot.CapturedAtUtc));

        var cards = cut.FindAll(".system-info-pulse__item.mo-card-surface");

        cards.Should().HaveCount(4);
        cards.Select(card => card.GetAttribute("data-mo-card-tone"))
            .Should().Equal("primary", "info", "neutral", "success");
        cards.Should().AllSatisfy(card =>
        {
            var rail = card.QuerySelector(".mo-card-surface__rail");
            rail.Should().NotBeNull();
            rail!.ParentElement.Should().BeSameAs(card);
            rail.GetAttribute("aria-hidden").Should().Be("true");
        });
    }
}
