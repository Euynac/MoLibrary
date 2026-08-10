using System.Globalization;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.Core.Localization.Abstractions;
using Monica.UI.Shell.Models;
using Monica.UI.Shell.Support;
using Xunit;

namespace Test.Monica.UI.Shell.Support;

public sealed class PageRegistryTests
{
    [Fact]
    public void RegisterLocalizedPage_WhenModuleOwnsResources_ShouldResolveThroughCurrentHostCatalog()
    {
        var registry = new PageRegistry();
        var categoryId = registry.RegisterLocalizedCategory<ThirdPartyResource>(
            "Tairitsua.Monica.GachaPool",
            "Navigation:Category",
            order: 450);
        registry.RegisterLocalizedPage<TestPage, ThirdPartyResource>(
            "/gacha-pool",
            "Navigation:Title",
            categoryId: categoryId,
            addToNav: true);
        registry.Seal();

        var page = registry.GetRegisteredPages().Single();
        var navigationItem = registry.GetNavItems().Single();
        var category = registry.GetNavigationCategories().Single(item => item.Id == categoryId);
        var firstHostCatalog = new HostLocalizationCatalog("first-host");
        var secondHostCatalog = new HostLocalizationCatalog("second-host");

        page.DisplayName.ResourceType.Should().Be(typeof(ThirdPartyResource));
        page.ResolveDisplayName(firstHostCatalog)
            .Should().Be("first-host:ThirdPartyResource:Navigation:Title");
        category.ResolveDisplayName(firstHostCatalog)
            .Should().Be("first-host:ThirdPartyResource:Navigation:Category");
        navigationItem.CategoryId.Should().Be(categoryId);
        navigationItem.ResolveText(firstHostCatalog)
            .Should().Be("first-host:ThirdPartyResource:Navigation:Title");
        navigationItem.ResolveText(secondHostCatalog)
            .Should().Be("second-host:ThirdPartyResource:Navigation:Title");
        firstHostCatalog.RequestedResourceTypes.Should().OnlyContain(type => type == typeof(ThirdPartyResource));
        secondHostCatalog.RequestedResourceTypes.Should().OnlyContain(type => type == typeof(ThirdPartyResource));
    }

    [Fact]
    public void RegisterLocalizedPage_WithAccessPolicy_ShouldPublishOnePolicyIdentityForPageAndNavigation()
    {
        var registry = new PageRegistry();
        registry.RegisterLocalizedPage<TestPage, ThirdPartyResource>(
            "protected",
            "Navigation:Title",
            addToNav: true,
            accessPolicyType: typeof(TestAccessPolicy));
        registry.Seal();

        var page = registry.GetRegisteredPages().Single();
        var navigation = registry.GetNavItems().Single();

        page.AccessPolicyType.Should().Be(typeof(TestAccessPolicy));
        navigation.Page.Should().BeSameAs(page);
    }

    [Fact]
    public void RegisterPage_WhenAccessPolicyTypeIsInvalid_ShouldRejectStartupRegistration()
    {
        var registry = new PageRegistry();

        var act = () => registry.RegisterPage<TestPage>(
            "invalid-policy",
            "Invalid policy",
            accessPolicyType: typeof(TestPage));

        act.Should().Throw<ArgumentException>()
            .WithParameterName("accessPolicyType");
    }

    [Fact]
    public void RegisterLocalizedCategory_WhenDefinitionMatches_ShouldBeIdempotentAcrossCasing()
    {
        var registry = new PageRegistry();

        var first = registry.RegisterLocalizedCategory<ThirdPartyResource>(
            "Tairitsua.Monica.GachaPool",
            "Navigation:Category",
            order: 450);
        var second = registry.RegisterLocalizedCategory<ThirdPartyResource>(
            "tairitsua.monica.gachapool",
            "Navigation:Category",
            order: 450);
        registry.Seal();

        second.Should().Be(first);
        registry.GetNavigationCategories().Count(item => item.Id == first).Should().Be(1);
    }

    [Fact]
    public void RegisterLocalizedCategory_WhenDefinitionConflicts_ShouldRejectCollision()
    {
        var registry = new PageRegistry();
        registry.RegisterLocalizedCategory<ThirdPartyResource>(
            "Tairitsua.Monica.GachaPool",
            "Navigation:Category",
            order: 450);

        var act = () => registry.RegisterLocalizedCategory<SecondResource>(
            "tairitsua.monica.gachapool",
            "Navigation:DifferentCategory",
            order: 451);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tairitsua.monica.gachapool*different label, resource, or order*");
    }

    [Fact]
    public void GetNavItems_WhenPageReferencesUnknownCategory_ShouldFailBeforeRouting()
    {
        var registry = new PageRegistry();
        registry.RegisterLocalizedPage<TestPage, ThirdPartyResource>(
            "gacha-pool",
            "Navigation:Title",
            categoryId: NavigationCategoryId.Create("Tairitsua.Monica.GachaPool"),
            addToNav: true);

        var act = registry.Seal;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unregistered categories*tairitsua.monica.gachapool*");
    }

    [Fact]
    public void RegisterPage_WhenRegistryIsSealed_ShouldRejectLateMutation()
    {
        var registry = new PageRegistry();
        registry.RegisterPage<TestPage>("first", "First page");
        registry.Seal();

        var act = () => registry.RegisterPage<SecondTestPage>("second", "Second page");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*sealed*application startup*");
    }

    [Fact]
    public void GetNavigationCategories_ShouldUseExplicitCultureIndependentOrder()
    {
        var registry = new PageRegistry();
        registry.RegisterLocalizedCategory<ThirdPartyResource>(
            "Tairitsua.Monica.GachaPool",
            "Navigation:Category",
            order: 450);
        registry.Seal();

        var ids = registry.GetNavigationCategories().Select(static category => category.Id).ToArray();

        ids.Should().ContainInOrder(
            BuiltInNavigationCategoryIds.AI,
            BuiltInNavigationCategoryIds.KnowledgeRetrieval,
            BuiltInNavigationCategoryIds.Documentation,
            BuiltInNavigationCategoryIds.Configuration,
            NavigationCategoryId.Create("Tairitsua.Monica.GachaPool"),
            BuiltInNavigationCategoryIds.Infrastructure,
            BuiltInNavigationCategoryIds.Module,
            BuiltInNavigationCategoryIds.TaskScheduling,
            BuiltInNavigationCategoryIds.Monitor,
            BuiltInNavigationCategoryIds.Debug,
            BuiltInNavigationCategoryIds.Uncategorized);
    }

    [Fact]
    public void ResolveText_WhenResourceDoesNotContainKey_ShouldUseRegisteredFallback()
    {
        var registry = new PageRegistry();
        registry.RegisterLocalizedPage<TestPage, ThirdPartyResource>(
            "gacha-pool",
            "Navigation:Title",
            addToNav: true);
        registry.Seal();
        var catalog = new HostLocalizationCatalog("host", resourceNotFound: true);
        var navigationItem = registry.GetNavItems().Single();

        navigationItem.ResolveText(catalog).Should().Be("Navigation:Title");
        navigationItem.CategoryId.Should().Be(BuiltInNavigationCategoryIds.Uncategorized);
    }

    [Fact]
    public void RegisterPage_WhenAnotherComponentOwnsTheRoute_ShouldRejectCollision()
    {
        var registry = new PageRegistry();
        registry.RegisterPage<TestPage>("/shared", "First page");

        var act = () => registry.RegisterPage<SecondTestPage>("SHARED", "Second page");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*/SHARED*TestPage*SecondTestPage*");
    }

    [Fact]
    public void ContributorContract_ShouldBeWriteOnlyAndCannotSealComposition()
    {
        var registry = new PageRegistry();
        INavigationRegistryBuilder contributor = registry;
        IPageCatalog catalog = registry;

        var readBeforeSeal = catalog.GetNavItems;

        readBeforeSeal.Should().Throw<InvalidOperationException>()
            .WithMessage("*not available*startup registration*");
        contributor.RegisterPage<TestPage>("first", "First page");
        registry.Seal();
        catalog.GetRegisteredPages().Should().ContainSingle();
        typeof(INavigationRegistryBuilder).GetMethods()
            .Should().OnlyContain(method => method.Name.StartsWith("Register", StringComparison.Ordinal));
    }

    [Fact]
    public void GetNavItems_WhenOrdersMatch_ShouldUseRouteAsDeterministicTieBreaker()
    {
        var registry = new PageRegistry();
        registry.RegisterPage<SecondTestPage>("zeta", "Zeta", addToNav: true, navOrder: 30);
        registry.RegisterPage<TestPage>("alpha", "Alpha", addToNav: true, navOrder: 30);
        registry.Seal();

        registry.GetNavItems().Select(static item => item.Href)
            .Should().ContainInOrder("alpha", "zeta");
    }

    [Fact]
    public void NavigationCategoryId_Create_ShouldCanonicalizeCasingAndHaveNoInvalidDefault()
    {
        var id = NavigationCategoryId.Create("Tairitsua.Monica.GachaPool");

        id.Value.Should().Be("tairitsua.monica.gachapool");
        id.Should().Be(NavigationCategoryId.Create("TAIRITSUA.MONICA.GACHAPOOL"));
        typeof(NavigationCategoryId).IsValueType.Should().BeFalse();
    }

    [Fact]
    public void CatalogModels_ShouldExposeImmutableState()
    {
        typeof(PageDefinition).GetProperties()
            .Should().OnlyContain(property => property.SetMethod == null);
        typeof(NavigationItem).GetProperties()
            .Should().OnlyContain(property => property.SetMethod == null);
    }

    private sealed class TestPage : ComponentBase;

    private sealed class SecondTestPage : ComponentBase;

    private sealed class ThirdPartyResource : ILocalizationResource;

    private sealed class SecondResource : ILocalizationResource;

    private sealed class TestAccessPolicy : IPageAccessPolicy
    {
        public Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

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
