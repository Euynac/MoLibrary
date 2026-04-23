using AwesomeAssertions;
using Microsoft.Extensions.Localization;
using Monica.UI.Localization;
using Monica.UI.Theming;
using Test.Monica.Localization;
using Xunit;

namespace Test.Monica.UI;

public class ThemeDisplayTextProviderTests
{
    private readonly IStringLocalizer<SharedResource> _localizer = new FoundStringLocalizer();

    [Fact]
    public void GetThemeOptions_ShouldReturnLocalizedNamesAndSimplifiedDescriptions()
    {
        var options = ThemeDisplayTextProvider.GetThemeOptions(_localizer);

        options.Should().Contain(option => option.Name == "default" && option.DisplayName == "Theme:Options:default:Name" && option.Description == "Theme:Options:default:Description");
        options.Should().Contain(option => option.Name == "classic-material" && option.DisplayName == "Theme:Options:classic-material:Name" && option.Description == "Theme:Options:classic-material:Description");
        options.Should().NotContain(option => option.Name == "clean-saas");
    }

    [Fact]
    public void GetCurrentThemeSummary_ShouldUseLocalizedThemeNameAndMode()
    {
        var summary = ThemeDisplayTextProvider.GetCurrentThemeSummary(_localizer, "default", isDarkMode: true);

        summary.Should().Be("Theme:Options:default:Name - Theme:Dark");
    }

    [Fact]
    public void GetCurrentThemeSummary_ShouldFallbackToInvariantName_WhenLocalizationKeyMissing()
    {
        var localizer = new MissingThemeNameLocalizer();

        var summary = ThemeDisplayTextProvider.GetCurrentThemeSummary(localizer, "ink-landscape", isDarkMode: false);

        summary.Should().Be("Ink Landscape - Theme:Light");
    }

    [Fact]
    public void GetThemeOptions_ShouldFallbackToEmptyDescription_WhenDescriptionKeyMissing()
    {
        var localizer = new MissingThemeDescriptionLocalizer();

        var options = ThemeDisplayTextProvider.GetThemeOptions(localizer);

        options.Should().Contain(option => option.Name == "default" && option.Description == string.Empty);
    }

    private class FoundStringLocalizer : EchoStringLocalizer<SharedResource>
    {
        public override LocalizedString this[string name] => new(name, name, resourceNotFound: false);
    }

    private sealed class MissingThemeNameLocalizer : FoundStringLocalizer
    {
        public override LocalizedString this[string name]
            => name == "Theme:Options:ink-landscape:Name"
                ? new LocalizedString(name, name, resourceNotFound: true)
                : base[name];
    }

    private sealed class MissingThemeDescriptionLocalizer : FoundStringLocalizer
    {
        public override LocalizedString this[string name]
            => name == "Theme:Options:default:Description"
                ? new LocalizedString(name, name, resourceNotFound: true)
                : base[name];
    }
}
