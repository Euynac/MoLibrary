using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Localization.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Localization;

public sealed class LocalizationProfileTests
{
    [Fact]
    public void Create_ShouldCanonicalizeCulturesAndFreezeCompleteDisplayNames()
    {
        var profile = LocalizationProfile.Create(
            " en-us ",
            ["EN-us", "zh-cn"],
            new Dictionary<string, string>
            {
                ["en-US"] = " English ",
                ["zh-CN"] = "简体中文"
            },
            " .Monica.Culture ");

        profile.DefaultCulture.Should().Be("en-US");
        profile.SupportedCultures.Should().Equal("en-US", "zh-CN");
        profile.CultureDisplayNames.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["en-US"] = "English",
            ["zh-CN"] = "简体中文"
        });
        profile.CookieName.Should().Be(".Monica.Culture");
        profile.ResolveCulture("unknown", "ZH-cn").Should().Be("zh-CN");
        profile.ResolveCulture("unknown").Should().Be("en-US");
    }

    [Theory]
    [InlineData(ProfileFailure.DuplicateCulture)]
    [InlineData(ProfileFailure.InvalidCulture)]
    [InlineData(ProfileFailure.UnsupportedDefault)]
    [InlineData(ProfileFailure.InvalidCookie)]
    [InlineData(ProfileFailure.UnsupportedDisplayName)]
    [InlineData(ProfileFailure.EmptyDisplayName)]
    public void Create_ShouldRejectInvalidComposition(ProfileFailure failure)
    {
        Action create = failure switch
        {
            ProfileFailure.DuplicateCulture => () => LocalizationProfile.Create(
                "en-US", ["en-US", "EN-us"], null, ".Culture"),
            ProfileFailure.InvalidCulture => () => LocalizationProfile.Create(
                "en-US", ["en-US", "not a culture!"], null, ".Culture"),
            ProfileFailure.UnsupportedDefault => () => LocalizationProfile.Create(
                "en-US", ["zh-CN"], null, ".Culture"),
            ProfileFailure.InvalidCookie => () => LocalizationProfile.Create(
                "en-US", ["en-US"], null, "bad cookie"),
            ProfileFailure.UnsupportedDisplayName => () => LocalizationProfile.Create(
                "en-US", ["en-US"], new Dictionary<string, string> { ["zh-CN"] = "Chinese" }, ".Culture"),
            ProfileFailure.EmptyDisplayName => () => LocalizationProfile.Create(
                "en-US", ["en-US"], new Dictionary<string, string> { ["en-US"] = " " }, ".Culture"),
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
        };

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_TwoHosts_ShouldPublishIndependentImmutableProfiles()
    {
        using var firstHost = BuildHost("en-US", "English", ".First.Culture");
        using var secondHost = BuildHost("zh-CN", "简体中文", ".Second.Culture");

        var first = firstHost.Services.GetRequiredService<LocalizationProfile>();
        var second = secondHost.Services.GetRequiredService<LocalizationProfile>();

        first.Should().NotBeSameAs(second);
        first.DefaultCulture.Should().Be("en-US");
        first.CookieName.Should().Be(".First.Culture");
        first.GetDisplayName("EN-us").Should().Be("English");
        second.DefaultCulture.Should().Be("zh-CN");
        second.CookieName.Should().Be(".Second.Culture");
        second.GetDisplayName("zh-cn").Should().Be("简体中文");
    }

    [Fact]
    public void AddMonica_WhenProfileIsInvalid_ShouldRejectDuringOptionFinalization()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
            monica.AddLocalization(options =>
            {
                options.DefaultCulture = "en-US";
                options.SupportedCultures = ["en-US", "EN-us"];
            }));

        compose.Should().Throw<Exception>()
            .WithMessage("*ModuleLocalization*option finalization*")
            .WithInnerException<ArgumentException>();
    }

    [Fact]
    public void Build_WhenMutableCompositionOptionsChange_ShouldKeepPublishedProfileUnchanged()
    {
        using var host = BuildHost("en-US", "English", ".Host.Culture");
        var profile = host.Services.GetRequiredService<LocalizationProfile>();
        var compositionInput = host.Services
            .GetRequiredService<IOptions<ModuleLocalizationOption>>()
            .Value;

        compositionInput.DefaultCulture = "zh-CN";
        compositionInput.SupportedCultures.Add("zh-CN");
        compositionInput.CultureDisplayNames["en-US"] = "Changed";

        profile.DefaultCulture.Should().Be("en-US");
        profile.SupportedCultures.Should().Equal("en-US");
        profile.GetDisplayName("en-US").Should().Be("English");
        profile.TryResolveSupportedCulture("zh-CN", out _).Should().BeFalse();
    }

    private static IHost BuildHost(string culture, string displayName, string cookieName)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddLocalization(options =>
            {
                options.DefaultCulture = culture;
                options.SupportedCultures = [culture];
                options.CultureDisplayNames = new Dictionary<string, string>
                {
                    [culture] = displayName
                };
                options.CookieName = cookieName;
            });
        });

        return builder.Build();
    }

    public enum ProfileFailure
    {
        DuplicateCulture,
        InvalidCulture,
        UnsupportedDefault,
        InvalidCookie,
        UnsupportedDisplayName,
        EmptyDisplayName
    }
}
