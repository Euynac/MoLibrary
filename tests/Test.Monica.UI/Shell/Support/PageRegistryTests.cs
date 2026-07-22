using System.Globalization;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.Core.Localization.Abstractions;
using Monica.UI.Localization;
using Monica.UI.Shell.Support;
using Xunit;

namespace Test.Monica.UI.Shell.Support;

public sealed class PageRegistryTests
{
    [Fact]
    public void RegisterLocalizedComponent_WhenModuleOwnsResource_ShouldResolveThroughCurrentHostCatalog()
    {
        var registry = new PageRegistry();
        registry.RegisterLocalizedComponent<TestPage, ThirdPartyResource>(
            "/gacha-pool",
            "Navigation:Title",
            categoryKey: "Navigation:Category",
            addToNav: true);
        var page = registry.GetRegisteredPages().Single();
        var navigationItem = registry.GetNavItems().Single();
        var firstHostCatalog = new HostLocalizationCatalog("first-host");
        var secondHostCatalog = new HostLocalizationCatalog("second-host");

        page.DisplayName.ResourceType.Should().Be(typeof(ThirdPartyResource));
        page.ResolveDisplayName(firstHostCatalog)
            .Should().Be("first-host:ThirdPartyResource:Navigation:Title");
        page.ResolveCategory(firstHostCatalog)
            .Should().Be("first-host:ThirdPartyResource:Navigation:Category");
        navigationItem.ResolveText(firstHostCatalog)
            .Should().Be("first-host:ThirdPartyResource:Navigation:Title");
        navigationItem.ResolveText(secondHostCatalog)
            .Should().Be("second-host:ThirdPartyResource:Navigation:Title");
        firstHostCatalog.RequestedResourceTypes.Should().OnlyContain(type => type == typeof(ThirdPartyResource));
        secondHostCatalog.RequestedResourceTypes.Should().OnlyContain(type => type == typeof(ThirdPartyResource));
    }

    [Fact]
    public void RegisterLocalizedComponent_WhenUsingBuiltInOverload_ShouldUseUIRegistryResource()
    {
        var registry = new PageRegistry();

        registry.RegisterLocalizedComponent<TestPage>(
            "module-system",
            "Pages:ModuleSystem:Title",
            addToNav: true);

        registry.GetRegisteredPages().Single().DisplayName.ResourceType
            .Should().Be(typeof(UIRegistryResource));
        registry.GetNavItems().Single().Text.ResourceType
            .Should().Be(typeof(UIRegistryResource));
    }

    [Fact]
    public void ResolveText_WhenResourceDoesNotContainKey_ShouldUseRegisteredFallback()
    {
        var registry = new PageRegistry();
        registry.RegisterLocalizedComponent<TestPage, ThirdPartyResource>(
            "gacha-pool",
            "Navigation:Title",
            categoryKey: "Navigation:Category",
            addToNav: true);
        var catalog = new HostLocalizationCatalog("host", resourceNotFound: true);
        var navigationItem = registry.GetNavItems().Single();

        navigationItem.ResolveText(catalog).Should().Be("Navigation:Title");
        navigationItem.ResolveCategory(catalog).Should().Be("Navigation:Category");
    }

    [Fact]
    public void RegisterComponent_WhenAnotherComponentOwnsTheRoute_ShouldRejectCollision()
    {
        var registry = new PageRegistry();
        registry.RegisterComponent<TestPage>("/shared", "First page");

        var act = () => registry.RegisterComponent<SecondTestPage>("SHARED", "Second page");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*/SHARED*TestPage*SecondTestPage*");
    }

    private sealed class TestPage : ComponentBase;

    private sealed class SecondTestPage : ComponentBase;

    private sealed class ThirdPartyResource : ILocalizationResource;

    private sealed class HostLocalizationCatalog(string hostName, bool resourceNotFound = false)
        : ILocalizationCatalog
    {
        public List<Type> RequestedResourceTypes { get; } = [];

        public IStringLocalizer For<TResource>() where TResource : class, ILocalizationResource
        {
            return For(typeof(TResource));
        }

        public IStringLocalizer For(Type resourceType)
        {
            RequestedResourceTypes.Add(resourceType);
            return new HostStringLocalizer(hostName, resourceType, resourceNotFound);
        }

        public string Get<TResource>(string key) where TResource : class, ILocalizationResource
        {
            return For<TResource>()[key].Value;
        }

        public string Get<TResource>(string key, params object[] arguments)
            where TResource : class, ILocalizationResource
        {
            return For<TResource>()[key, arguments].Value;
        }
    }

    private sealed class HostStringLocalizer(string hostName, Type resourceType, bool resourceNotFound)
        : IStringLocalizer
    {
        public LocalizedString this[string name] => new(
            name,
            $"{hostName}:{resourceType.Name}:{name}",
            resourceNotFound);

        public LocalizedString this[string name, params object[] arguments] => new(
            name,
            $"{hostName}:{resourceType.Name}:{string.Format(CultureInfo.InvariantCulture, name, arguments)}",
            resourceNotFound);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return [];
        }

        public IStringLocalizer WithCulture(CultureInfo culture)
        {
            return this;
        }
    }
}
