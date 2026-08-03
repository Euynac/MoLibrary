using AwesomeAssertions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Facades;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Core.Results;
using NSubstitute;
using Xunit;

namespace Test.Monica.Configuration.Services;

public sealed class ConfigurationDefinitionChangeImpactServiceTests
{
    [Fact]
    public async Task GetImpactAsync_WhenPublishersOverlap_ShouldReturnDeterministicLogicalServiceUnion()
    {
        var metadataStore = Substitute.For<IConfigurationMetadataStore>();
        metadataStore.GetDefinitionPublisherStatesAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, IReadOnlyList<ConfigurationDefinitionPublisherState>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["A.Definition"] =
                [
                    CreateState(
                        "service.shared",
                        ConfigurationReloadBehaviorObservationKind.Inferred,
                        ConfigurationReloadBehavior.OnlineReloadable),
                    CreateState(
                        "Service.Neutral",
                        ConfigurationReloadBehaviorObservationKind.NotConsumed,
                        ConfigurationReloadBehavior.Unknown)
                ],
                ["Z.Definition"] =
                [
                    CreateState(
                        "Service.Shared",
                        ConfigurationReloadBehaviorObservationKind.Unresolved,
                        ConfigurationReloadBehavior.Unknown),
                    CreateState(
                        "Service.Second",
                        ConfigurationReloadBehaviorObservationKind.Declared,
                        ConfigurationReloadBehavior.RequiresRestart)
                ],
                ["Only.Neutral"] =
                [
                    CreateState(
                        "Service.Neutral",
                        ConfigurationReloadBehaviorObservationKind.NotConsumed,
                        ConfigurationReloadBehavior.Unknown)
                ],
                ["No.State"] = []
            });
        var service = new ConfigurationDefinitionChangeImpactService(metadataStore);

        var impact = await service.GetImpactAsync(
            [" Z.Definition ", "A.Definition", "a.definition", "No.State", "Only.Neutral"],
            TestContext.Current.CancellationToken);

        impact.DefinitionKeys.Should().Equal(
            "A.Definition",
            "No.State",
            "Only.Neutral",
            "Z.Definition");
        impact.AffectedPublishers.Select(static publisher => publisher.PublisherKey)
            .Should().Equal("Service.Second", "Service.Shared");
        impact.AffectedPublishers.Single(static publisher =>
                publisher.PublisherKey == "Service.Shared")
            .DefinitionKeys.Should().Equal("A.Definition", "Z.Definition");
        impact.DefinitionsWithoutKnownConsumers.Should().Equal("No.State", "Only.Neutral");
    }

    [Fact]
    public async Task GetImpactAsync_WhenDefinitionKeysAreEmpty_ShouldRejectRequestBeforeReadingStore()
    {
        var metadataStore = Substitute.For<IConfigurationMetadataStore>();
        var service = new ConfigurationDefinitionChangeImpactService(metadataStore);

        var act = () => service.GetImpactAsync([" ", ""], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one non-empty definition key*");
        await metadataStore.DidNotReceive().GetDefinitionPublisherStatesAsync(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetImpactAsync_WhenStoreOmitsRequestedDefinition_ShouldFailClosed()
    {
        var metadataStore = Substitute.For<IConfigurationMetadataStore>();
        metadataStore.GetDefinitionPublisherStatesAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, IReadOnlyList<ConfigurationDefinitionPublisherState>>());
        var service = new ConfigurationDefinitionChangeImpactService(metadataStore);

        var act = () => service.GetImpactAsync(
            ["Test.Omitted"],
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*omitted publisher state*Test.Omitted*");
    }

    [Fact]
    public async Task GetDefinitionChangeImpactAsync_WhenStoreFails_ShouldReturnRecursiveDiagnostic()
    {
        var impactService = Substitute.For<IConfigurationDefinitionChangeImpactService>();
        impactService.GetImpactAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ConfigurationDefinitionChangeImpact>(new InvalidOperationException(
                "outer impact failure",
                new InvalidDataException("inner metadata failure"))));
        var facade = CreateFacade(impactService);

        var result = await facade.GetDefinitionChangeImpactAsync(["Test.Definition"]);

        result.IsFailed(out var error, out _).Should().BeTrue();
        error!.Message.Should().Contain("outer impact failure");
        error.Message.Should().Contain("inner metadata failure");
    }

    private static ConfigurationDefinitionPublisherState CreateState(
        string publisherKey,
        ConfigurationReloadBehaviorObservationKind observationKind,
        ConfigurationReloadBehavior reloadBehavior)
    {
        return new ConfigurationDefinitionPublisherState
        {
            PublisherKey = publisherKey,
            ObservationKind = observationKind,
            ReloadBehavior = reloadBehavior
        };
    }

    private static ConfigurationFacade CreateFacade(
        IConfigurationDefinitionChangeImpactService definitionChangeImpactService)
    {
        return new ConfigurationFacade(
            definitionResolver: null!,
            definitionRegistry: null!,
            definitionMaintenanceStore: null!,
            definitionChangeImpactService,
            mutationGroupApplyService: null!,
            historyService: null!,
            mutationGroupService: null!,
            rollbackService: null!,
            unifiedVersionService: null!,
            effectiveValueStore: null!,
            historyStore: null!,
            metadataStore: null!,
            changeNotifiers: [],
            storeStateTracker: null!,
            effectiveStateReader: null!,
            sourceInspector: null!,
            sourceWriter: null!,
            runtimeContext: null!,
            runtimeValidationService: null!,
            candidateValidationService: null!,
            runtimeReloadService: null!,
            reloadBroadcastService: null!);
    }
}
