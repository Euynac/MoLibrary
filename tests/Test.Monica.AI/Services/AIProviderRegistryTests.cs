using AwesomeAssertions;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.Services;
using NSubstitute;

namespace Test.Monica.AI.Services;

public sealed class AIProviderRegistryTests
{
    [Fact]
    public void Constructor_WhenProvidersAreValid_ShouldCreateImmutableSnapshotAndResolveDefault()
    {
        var providers = new List<IAIProvider>
        {
            CreateProvider("secondary", isDefault: false),
            CreateProvider("primary", isDefault: true)
        };

        var registry = new AIProviderRegistry(providers);
        providers.Clear();

        registry.GetAllProviderInfos().Select(static info => info.ProviderId)
            .Should().Equal("secondary", "primary");
        registry.GetDefaultProvider()!.ProviderId.Should().Be("primary");
    }

    [Fact]
    public void Constructor_WhenProviderIdsAreDuplicated_ShouldRejectAmbiguousRegistration()
    {
        var providers = new[]
        {
            CreateProvider("duplicate", isDefault: false),
            CreateProvider("DUPLICATE", isDefault: false)
        };

        var action = () => new AIProviderRegistry(providers);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate AI provider identifiers*");
    }

    private static IAIProvider CreateProvider(string providerId, bool isDefault)
    {
        var provider = Substitute.For<IAIProvider>();
        provider.ProviderId.Returns(providerId);
        provider.Info.Returns(new AIProviderInfo
        {
            ProviderId = providerId,
            DisplayName = providerId,
            ProviderType = "Test",
            IsDefault = isDefault,
            IsValid = true
        });
        return provider;
    }
}
