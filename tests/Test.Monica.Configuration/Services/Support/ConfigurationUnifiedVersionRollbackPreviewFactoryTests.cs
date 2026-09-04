using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Projection;
using Monica.Configuration.Serialization;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using NSubstitute;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public sealed class ConfigurationUnifiedVersionRollbackPreviewFactoryTests
{
    [Fact]
    public async Task CreateAsync_WhenSnapshotContainsAvailableAndMissingDefinitions_ShouldUseOneBulkReadAndPreserveTargetOrder()
    {
        var firstDefinition = CreateDefinition("Definition.First", "First");
        var secondDefinition = CreateDefinition("Definition.Second", "Second");
        var firstDocument = CreateEffectiveDocument(firstDefinition, workerId: 3, version: 11);
        var secondDocument = CreateEffectiveDocument(secondDefinition, workerId: 1, version: 21);
        var fixture = CreateFixture(
            [firstDefinition, secondDefinition],
            [firstDocument, secondDocument],
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [firstDefinition.DefinitionKey] = 3,
                [secondDefinition.DefinitionKey] = 1
            });
        var snapshot = CreateSnapshot(
            (secondDefinition, "{\"WorkerId\":2}"),
            (CreateDefinition("Definition.Missing", "Missing"), "{\"WorkerId\":4}"),
            (firstDefinition, "{\"WorkerId\":3}"));

        var preview = await fixture.Factory.CreateAsync(snapshot, TestContext.Current.CancellationToken);

        preview.Targets.Select(static target => target.DefinitionKey).Should().Equal(
            secondDefinition.DefinitionKey,
            "Definition.Missing",
            firstDefinition.DefinitionKey);
        preview.Targets.Select(static target => target.Status).Should().Equal(
            ConfigurationUnifiedVersionApplyTargetStatus.Ready,
            ConfigurationUnifiedVersionApplyTargetStatus.MissingDefinition,
            ConfigurationUnifiedVersionApplyTargetStatus.Unchanged);
        preview.Targets[0].Mutations.Should().ContainSingle().Which.ExpectedValueVersion.Should().Be(21);
        preview.SkippedDefinitionKeys.Should().Equal("Definition.Missing");
        preview.SkippedCount.Should().Be(1);
        preview.ChangeCount.Should().Be(1);
        preview.BlockedCount.Should().Be(0);
        preview.CanApply.Should().BeTrue();
        preview.PreviewFingerprint.Should().Be(
            ConfigurationUnifiedVersionRollbackPreviewFingerprint.Compute(snapshot.Summary.Version, preview.Targets));
        await fixture.EffectiveValueStore.Received(1).GetManyAsync(
            Arg.Is<IReadOnlyList<string>>(keys => keys.SequenceEqual(new[]
            {
                secondDefinition.DefinitionKey,
                firstDefinition.DefinitionKey
            })),
            TestContext.Current.CancellationToken);
        await fixture.EffectiveValueStore.DidNotReceive().GetAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenPersistedValueDiffersFromRuntime_ShouldReportSourceDriftWithoutPerDefinitionRead()
    {
        var definition = CreateDefinition("Definition.Drift", "Drift");
        var persistedDocument = CreateEffectiveDocument(definition, workerId: 0, version: 31);
        var fixture = CreateFixture(
            [definition],
            [persistedDocument],
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [definition.DefinitionKey] = 1
            });
        var snapshot = CreateSnapshot((definition, "{\"WorkerId\":1}"));

        var preview = await fixture.Factory.CreateAsync(snapshot, TestContext.Current.CancellationToken);

        var target = preview.Targets.Should().ContainSingle().Which;
        target.Status.Should().Be(ConfigurationUnifiedVersionApplyTargetStatus.RuntimeOutOfSync);
        var drift = target.Mutations.Should().ContainSingle().Which;
        drift.Status.Should().Be(ConfigurationUnifiedVersionApplyMutationStatus.RuntimeOutOfSync);
        drift.LogicalPath.Should().Be("WorkerId");
        drift.CurrentJson.Should().Be("0");
        drift.TargetJson.Should().Be("1");
        drift.ExpectedValueVersion.Should().Be(31);
        await fixture.EffectiveValueStore.Received(1).GetManyAsync(
            Arg.Is<IReadOnlyList<string>>(keys => keys.SequenceEqual(new[] { definition.DefinitionKey })),
            TestContext.Current.CancellationToken);
        await fixture.EffectiveValueStore.DidNotReceive().GetAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenUnrelatedPublishedMetadataIsUnavailable_ShouldResolveOnlyRollbackTargets()
    {
        var definition = CreateDefinition("Definition.Healthy", "Healthy");
        var document = CreateEffectiveDocument(definition, workerId: 1, version: 41);
        var metadataStore = Substitute.For<IConfigurationMetadataStore>();
        metadataStore.GetPublishedDefinitionEntryAsync(
                definition.DefinitionKey,
                Arg.Any<CancellationToken>())
            .Returns((ConfigurationPublishedDefinitionEntry?)null);
        metadataStore.ListPublishedDefinitionEntriesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ConfigurationPublishedDefinitionEntry>>(
                new InvalidDataException("unrelated corrupt metadata")));
        var fixture = CreateFixture(
            [definition],
            [document],
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [definition.DefinitionKey] = 1
            },
            metadataStore);

        var preview = await fixture.Factory.CreateAsync(
            CreateSnapshot((definition, "{\"WorkerId\":1}")),
            TestContext.Current.CancellationToken);

        preview.Targets.Should().ContainSingle().Which.Status
            .Should().Be(ConfigurationUnifiedVersionApplyTargetStatus.Unchanged);
        await metadataStore.DidNotReceive().ListPublishedDefinitionEntriesAsync(
            Arg.Any<CancellationToken>());
    }

    private static PreviewFactoryFixture CreateFixture(
        IReadOnlyList<ConfigurationDefinition> definitions,
        IReadOnlyList<ConfigurationEffectiveValueDocument> documents,
        IReadOnlyDictionary<string, int> runtimeWorkerIds,
        IConfigurationMetadataStore? metadataStore = null)
    {
        var registry = new ConfigurationDefinitionRegistry();
        registry.RegisterRange(definitions);
        var definitionResolver = new ConfigurationDefinitionResolver(registry, metadataStore);

        var documentsByKey = documents.ToDictionary(
            static document => document.DefinitionKey,
            StringComparer.OrdinalIgnoreCase);
        var effectiveValueStore = Substitute.For<IConfigurationEffectiveValueStore>();
        effectiveValueStore.GetManyAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyList<string>>(0)
                .Select(key => documentsByKey.GetValueOrDefault(key))
                .ToArray());

        var configurationValues = definitions.ToDictionary(
            definition => $"{definition.SectionPath}:WorkerId",
            definition => (string?)runtimeWorkerIds[definition.DefinitionKey].ToString(CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
        var runtimeContext = new ConfigurationRuntimeContext();
        runtimeContext.Capture(new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build());
        var reloadCoordinator = new TestReloadCoordinator(documentsByKey);
        var effectiveSnapshotReader = new ConfigurationEffectiveSnapshotReader(
            effectiveValueStore,
            new ConfigurationEffectiveValueSeedFactory(runtimeContext),
            reloadCoordinator);

        var monicaSource = new ConfigurationSourceDescriptor
        {
            SourceKey = "monica:effective",
            PriorityIndex = 1,
            DisplayName = "Monica Effective Store",
            ProviderType = "TestMonicaConfigurationProvider",
            Kind = ConfigurationSourceKind.MonicaEffectiveStore,
            IsManagedByMonica = true,
            IsWritable = true
        };
        var sourceInspector = Substitute.For<IConfigurationSourceInspector>();
        sourceInspector.GetSources().Returns([monicaSource]);
        sourceInspector.GetSourceChain(
                Arg.Any<ConfigurationDefinition>(),
                Arg.Any<LogicalPath>())
            .Returns(call =>
            {
                var definition = call.ArgAt<ConfigurationDefinition>(0);
                var logicalPath = call.ArgAt<LogicalPath>(1);
                return new ConfigurationSourceChain
                {
                    DefinitionKey = definition.DefinitionKey,
                    LogicalPath = logicalPath,
                    ConfigurationPath = $"{definition.SectionPath}:{logicalPath}",
                    Values =
                    [
                        new ConfigurationSourceValue
                        {
                            Source = monicaSource,
                            DisplayValue = "runtime",
                            IsEffective = true
                        }
                    ]
                };
            });
        var persistencePlanner = new ConfigurationRollbackPersistencePlanner(
            sourceInspector,
            null!,
            new ConfigurationEffectiveValueDocumentEditor(
                new ConfigurationEffectiveValuePatchEngine(),
                new ConfigurationStoredValueCodec()),
            new ConfigurationPathProjector(),
            new MonicaConfigurationProviderAccessor());
        var factory = new ConfigurationUnifiedVersionRollbackPreviewFactory(
            definitionResolver,
            effectiveSnapshotReader,
            new ConfigurationValidationCoordinator(new ConfigurationValueValidationEngine()),
            persistencePlanner);
        return new PreviewFactoryFixture(factory, effectiveValueStore);
    }

    private static ConfigurationDefinition CreateDefinition(string definitionKey, string sectionPath)
    {
        var definition = TestConfigurationFactory.Definition() with
        {
            DefinitionKey = definitionKey,
            SectionPath = sectionPath,
            DisplayName = definitionKey,
            SchemaVersion = 1,
            DefinitionRevision = 1
        };
        return definition with
        {
            SchemaHash = ConfigurationDefinitionSchemaCodec.ComputeSchemaHash(
                definition.DefinitionKey,
                definition.SectionPath,
                definition.Root)
        };
    }

    private static ConfigurationEffectiveValueDocument CreateEffectiveDocument(
        ConfigurationDefinition definition,
        int workerId,
        long version)
    {
        return new ConfigurationEffectiveValueDocument
        {
            DefinitionKey = definition.DefinitionKey,
            Json = $"{{\"WorkerId\":{workerId}}}",
            Version = version,
            SchemaVersion = definition.SchemaVersion
        };
    }

    private static ConfigurationUnifiedVersionSnapshot CreateSnapshot(
        params (ConfigurationDefinition Definition, string Json)[] definitions)
    {
        return new ConfigurationUnifiedVersionSnapshot
        {
            Summary = new ConfigurationUnifiedVersionSummary { Version = 17 },
            Definitions = definitions.Select(item => new ConfigurationUnifiedVersionDefinitionSnapshot
            {
                DefinitionKey = item.Definition.DefinitionKey,
                DisplayName = item.Definition.DisplayName,
                FromProject = item.Definition.FromProject,
                SchemaVersion = item.Definition.SchemaVersion,
                SchemaHash = item.Definition.SchemaHash,
                Json = item.Json
            }).ToArray()
        };
    }

    private sealed record PreviewFactoryFixture(
        ConfigurationUnifiedVersionRollbackPreviewFactory Factory,
        IConfigurationEffectiveValueStore EffectiveValueStore);

    private sealed class TestReloadCoordinator(
        IReadOnlyDictionary<string, ConfigurationEffectiveValueDocument> documentsByKey)
        : IConfigurationReloadCoordinator
    {
        public Task ReloadMonicaProjectionAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ReloadMonicaProjectionAsync(
            string definitionKey,
            long? minimumVersion,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public long? GetLoadedMonicaProjectionVersion(string definitionKey)
        {
            return documentsByKey.GetValueOrDefault(definitionKey)?.Version;
        }

        public Task ReloadRuntimeConfigurationAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
