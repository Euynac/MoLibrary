using AwesomeAssertions;
using Microsoft.Extensions.Localization;
using Monica.UI.Localization;
using Monica.UI.Theming;
using Monica.Testing.Localization;
using Xunit;

namespace Test.Monica.UI;

public class ThemeDisplayTextProviderTests
{
    private readonly IStringLocalizer<SharedResource> _localizer = new FoundStringLocalizer();

    [Fact]
    public void GetThemeOptions_ShouldReturnLocalizedNamesAndSimplifiedDescriptions()
    {
        var options = ThemeDisplayTextProvider.GetThemeOptions(_localizer);

        options.Should().Contain(option => option.Kind == MonicaThemeKind.Default && option.DisplayName == "Theme:Options:Default:Name" && option.Description == "Theme:Options:Default:Description");
        options.Should().Contain(option => option.Kind == MonicaThemeKind.MaterialDesign3 && option.DisplayName == "Theme:Options:MaterialDesign3:Name" && option.Description == "Theme:Options:MaterialDesign3:Description");
        options.Should().NotContain(option => option.DisplayName == "Theme:Options:clean-saas:Name");
    }

    [Fact]
    public void GetCurrentThemeSummary_ShouldUseLocalizedThemeNameAndMode()
    {
        var summary = ThemeDisplayTextProvider.GetCurrentThemeSummary(_localizer, MonicaThemeKind.Default, isDarkMode: true);

        summary.Should().Be("Theme:Options:Default:Name - Theme:Dark");
    }

    [Fact]
    public void GetCurrentThemeSummary_ShouldFallbackToInvariantName_WhenLocalizationKeyMissing()
    {
        var localizer = new MissingThemeNameLocalizer();

        var summary = ThemeDisplayTextProvider.GetCurrentThemeSummary(localizer, MonicaThemeKind.InkLandscape, isDarkMode: false);

        summary.Should().Be("Ink Landscape - Theme:Light");
    }

    [Fact]
    public void GetThemeOptions_ShouldFallbackToEmptyDescription_WhenDescriptionKeyMissing()
    {
        var localizer = new MissingThemeDescriptionLocalizer();

        var options = ThemeDisplayTextProvider.GetThemeOptions(localizer);

        options.Should().Contain(option => option.Kind == MonicaThemeKind.Default && option.Description == string.Empty);
    }

    private class FoundStringLocalizer : EchoStringLocalizer<SharedResource>
    {
        public override LocalizedString this[string name] => new(name, name, resourceNotFound: false);
    }

    private sealed class MissingThemeNameLocalizer : FoundStringLocalizer
    {
        public override LocalizedString this[string name]
            => name == "Theme:Options:InkLandscape:Name"
                ? new LocalizedString(name, name, resourceNotFound: true)
                : base[name];
    }

    private sealed class MissingThemeDescriptionLocalizer : FoundStringLocalizer
    {
        public override LocalizedString this[string name]
            => name == "Theme:Options:Default:Description"
                ? new LocalizedString(name, name, resourceNotFound: true)
                : base[name];
    }
}
