using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Annotations;
using Monica.Configuration.Bootstrap;
using Monica.Configuration.Models;
using Monica.Core.Modularity.Abstractions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.Bootstrap;

public sealed class MonicaConfigurationInputPlanTests
{
    private const string DEFINITION_KEY = "test.configuration.input-plan";

    [Fact]
    public void Create_WhenStoreIsMissing_ShouldRejectPlan()
    {
        Action create = () => MonicaConfigurationInputPlan.Create(inputs => inputs
            .AddManagedJsonFile("appsettings.json"));

        create.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly one store composition*");
    }

    [Fact]
    public void Create_WhenSecondStoreIsSelected_ShouldRejectAmbiguousComposition()
    {
        var first = new RecordingStoreComposition("First");
        var second = new RecordingStoreComposition("Second");

        Action create = () => MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(first)
            .UseConfigurationStore(second));

        create.Should().Throw<InvalidOperationException>()
            .WithMessage("*First*Second*");
    }

    [Fact]
    public void Create_WhenManagedJsonPathDiffersOnlyByCase_ShouldRejectDuplicateSource()
    {
        var composition = new RecordingStoreComposition("Recording");

        Action create = () => MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(composition)
            .AddManagedJsonFile("settings.json")
            .AddManagedJsonFile("SETTINGS.JSON"));

        create.Should().Throw<InvalidOperationException>()
            .WithMessage("*already declared*");
    }

    [Fact]
    public void Create_WhenSectionPathConventionIsDeclaredTwice_ShouldRejectAmbiguousConvention()
    {
        var composition = new RecordingStoreComposition("Recording");

        Action create = () => MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(composition)
            .UseSectionPathConvention(ConfigurationSectionPathConvention.ClrFullName)
            .UseSectionPathConvention(ConfigurationSectionPathConvention.ShortTypeName));

        create.Should().Throw<InvalidOperationException>()
            .WithMessage("*already declared*");
    }

    [Fact]
    public void Create_WhenDeclarationBuilderEscapes_ShouldSealBuilder()
    {
        MonicaConfigurationInputPlanBuilder? declaration = null;
        _ = MonicaConfigurationInputPlan.Create(inputs =>
        {
            declaration = inputs;
            inputs.UseConfigurationStore(new RecordingStoreComposition("Recording"));
        });

        Action mutate = () => declaration!.AddManagedJsonFile("late.json");

        mutate.Should().Throw<InvalidOperationException>()
            .WithMessage("*sealed*");
    }

    [Fact]
    public async Task BuildBootstrapConfiguration_WhenSourcesOverlap_ShouldPreserveDeclarationOrderWithoutMutatingHost()
    {
        using var directory = new TemporaryConfigurationDirectory();
        await directory.WriteJsonAsync(
            "global.json",
            """
            {
              "Layered": {
                "Winner": "global",
                "GlobalOnly": "global"
              }
            }
            """);
        await directory.WriteJsonAsync(
            "app.json",
            """
            {
              "Layered": {
                "Winner": "app",
                "AppOnly": "app"
              }
            }
            """);
        var composition = new RecordingStoreComposition("Recording");
        var inputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(composition)
            .AddManagedJsonFile("global.json", optional: false, reloadOnChange: false)
            .AddManagedJsonFile("app.json", optional: false, reloadOnChange: false));
        var hostBuilder = directory.CreateHostBuilder();
        hostBuilder.Configuration["Layered:Winner"] = "host";
        hostBuilder.Configuration["Layered:HostOnly"] = "host";

        using var bootstrap = inputPlan.BuildBootstrapConfiguration(hostBuilder);

        bootstrap["Layered:Winner"].Should().Be("app");
        bootstrap["Layered:HostOnly"].Should().Be("host");
        bootstrap["Layered:GlobalOnly"].Should().Be("global");
        bootstrap["Layered:AppOnly"].Should().Be("app");
        hostBuilder.Configuration["Layered:Winner"].Should().Be("host");
        composition.CreatedStores.Should().BeEmpty();
        bootstrap.Providers
            .OfType<JsonConfigurationProvider>()
            .Select(static provider => provider.Source.Path)
            .Should().Equal("global.json", "app.json");
    }

    [Fact]
    public async Task BuildBootstrapConfiguration_WhenPlansAreIndependent_ShouldNotLeakManagedSources()
    {
        using var directory = new TemporaryConfigurationDirectory();
        await directory.WriteJsonAsync("first.json", """{"Isolation":{"Value":"first"}}""");
        await directory.WriteJsonAsync("second.json", """{"Isolation":{"Value":"second"}}""");
        var firstPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(new RecordingStoreComposition("First"))
            .AddManagedJsonFile("first.json", optional: false, reloadOnChange: false));
        var secondPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(new RecordingStoreComposition("Second"))
            .AddManagedJsonFile("second.json", optional: false, reloadOnChange: false));
        var hostBuilder = directory.CreateHostBuilder();

        using var firstBootstrap = firstPlan.BuildBootstrapConfiguration(hostBuilder);
        using var secondBootstrap = secondPlan.BuildBootstrapConfiguration(hostBuilder);

        firstBootstrap["Isolation:Value"].Should().Be("first");
        secondBootstrap["Isolation:Value"].Should().Be("second");
        firstBootstrap.Providers.OfType<JsonConfigurationProvider>()
            .Should().ContainSingle(provider => provider.Source.Path == "first.json");
        secondBootstrap.Providers.OfType<JsonConfigurationProvider>()
            .Should().ContainSingle(provider => provider.Source.Path == "second.json");
    }

    [Fact]
    public async Task EnsureEffectiveOptionsSnapshotAsync_WhenLayersOverlap_ShouldApplyPrecedenceAndDisposeStore()
    {
        using var directory = new TemporaryConfigurationDirectory();
        await directory.WriteJsonAsync(
            "global.json",
            """
            {
              "InputPlan": {
                "GlobalWinsStore": "global",
                "AppWinsGlobal": "global"
              }
            }
            """);
        await directory.WriteJsonAsync(
            "app.json",
            """
            {
              "InputPlan": {
                "AppWinsGlobal": "app"
              }
            }
            """);
        var composition = new RecordingStoreComposition(
            "Recording",
            """
            {
              "StoreWinsBootstrap": "store",
              "GlobalWinsStore": "store",
              "AppWinsGlobal": "store"
            }
            """);
        var inputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(composition)
            .AddManagedJsonFile("global.json", optional: false, reloadOnChange: false)
            .AddManagedJsonFile("app.json", optional: false, reloadOnChange: false));
        var hostBuilder = directory.CreateHostBuilder();
        hostBuilder.Configuration["InputPlan:StoreWinsBootstrap"] = "bootstrap";
        hostBuilder.Configuration["InputPlan:GlobalWinsStore"] = "bootstrap";
        hostBuilder.Configuration["InputPlan:AppWinsGlobal"] = "bootstrap";
        using var bootstrap = inputPlan.BuildBootstrapConfiguration(hostBuilder);

        var snapshot = await inputPlan.EnsureEffectiveOptionsSnapshotAsync(
            bootstrap,
            [typeof(InputPlanOptions)],
            cancellationToken: TestContext.Current.CancellationToken);

        var options = snapshot.Get<InputPlanOptions>();
        options.StoreWinsBootstrap.Should().Be("store");
        options.GlobalWinsStore.Should().Be("global");
        options.AppWinsGlobal.Should().Be("app");
        composition.CreatedStores.Should().ContainSingle();
        composition.CreatedStores[0].BatchEnsureCount.Should().Be(1);
        composition.CreatedStores[0].DisposeCount.Should().Be(0);
        composition.CreatedStores[0].AsyncDisposeCount.Should().Be(1);
    }

    [Fact]
    public void EnsureEffectiveOptionsSnapshot_WhenPlanUsesClrFullName_ShouldBindConventionAndDisposeStoreSynchronously()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var composition = new RecordingStoreComposition("Recording");
        var inputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(composition)
            .UseSectionPathConvention(ConfigurationSectionPathConvention.ClrFullName));
        var hostBuilder = directory.CreateHostBuilder();
        var sectionPath = typeof(ConventionOptions).FullName!.Replace('.', ':');
        hostBuilder.Configuration[$"{sectionPath}:Value"] = "full-name";
        using var bootstrap = inputPlan.BuildBootstrapConfiguration(hostBuilder);

        var snapshot = inputPlan.EnsureEffectiveOptionsSnapshot(
            bootstrap,
            [typeof(ConventionOptions)]);

        snapshot.Get<ConventionOptions>().Value.Should().Be("full-name");
        composition.CreatedStores.Should().ContainSingle();
        composition.CreatedStores[0].DisposeCount.Should().Be(1);
        composition.CreatedStores[0].AsyncDisposeCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureEffectiveOptionsSnapshotAsync_WhenStoreReadFails_ShouldDisposeStore()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var composition = new RecordingStoreComposition(
            "Failing",
            failure: new InvalidOperationException("Store read failed."));
        var inputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(composition));
        var hostBuilder = directory.CreateHostBuilder();
        using var bootstrap = inputPlan.BuildBootstrapConfiguration(hostBuilder);

        Func<Task> ensure = () => inputPlan.EnsureEffectiveOptionsSnapshotAsync(
            bootstrap,
            [typeof(InputPlanOptions)],
            cancellationToken: TestContext.Current.CancellationToken);

        await ensure.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Store read failed.");
        composition.CreatedStores.Should().ContainSingle();
        composition.CreatedStores[0].AsyncDisposeCount.Should().Be(1);
    }

    [Fact]
    public void EnsureEffectiveOptionsSnapshot_WhenOptionsTypesAreEmpty_ShouldRejectBeforeCreatingStore()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var composition = new RecordingStoreComposition("Recording");
        var inputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(composition));
        var hostBuilder = directory.CreateHostBuilder();
        using var bootstrap = inputPlan.BuildBootstrapConfiguration(hostBuilder);

        Action ensure = () => inputPlan.EnsureEffectiveOptionsSnapshot(
            bootstrap,
            Array.Empty<Type>());

        ensure.Should().Throw<ArgumentException>()
            .WithParameterName("optionsTypes");
        composition.CreatedStores.Should().BeEmpty();
    }

    [Fact]
    public void EnsureEffectiveOptionsSnapshot_WhenBootstrapBelongsToAnotherPlan_ShouldRejectBeforeCreatingStore()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var firstPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(new RecordingStoreComposition("First")));
        var secondComposition = new RecordingStoreComposition("Second");
        var secondPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseConfigurationStore(secondComposition));
        using var bootstrap = firstPlan.BuildBootstrapConfiguration(directory.CreateHostBuilder());

        Action ensure = () => secondPlan.EnsureEffectiveOptionsSnapshot(
            bootstrap,
            [typeof(InputPlanOptions)]);

        ensure.Should().Throw<InvalidOperationException>()
            .WithMessage("*different*input plan*");
        secondComposition.CreatedStores.Should().BeEmpty();
    }

    [Configuration("InputPlan", DefinitionKey = DEFINITION_KEY)]
    private sealed class InputPlanOptions
    {
        public string StoreWinsBootstrap { get; set; } = string.Empty;

        public string GlobalWinsStore { get; set; } = string.Empty;

        public string AppWinsGlobal { get; set; } = string.Empty;
    }

    [Configuration(DefinitionKey = "test.configuration.input-plan-convention")]
    private sealed class ConventionOptions
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class RecordingStoreComposition(
        string name,
        string? effectiveJson = null,
        Exception? failure = null)
        : IMonicaConfigurationStoreComposition
    {
        public string Name { get; } = name;

        public List<RecordingEffectiveValueStore> CreatedStores { get; } = [];

        public void ConfigureRuntime(
            ModuleRegistration<ModuleConfiguration, ModuleConfigurationOption> module)
        {
        }

        public IConfigurationEffectiveValueStore CreateStartupStore()
        {
            var store = new RecordingEffectiveValueStore(effectiveJson, failure);
            CreatedStores.Add(store);
            return store;
        }
    }

    private sealed class RecordingEffectiveValueStore(
        string? effectiveJson,
        Exception? failure)
        : IConfigurationEffectiveValueStore, IDisposable, IAsyncDisposable
    {
        public ConfigurationStoreDescriptor Descriptor { get; } = new()
        {
            StoreKey = "test:recording",
            DisplayName = "Recording",
            Kind = ConfigurationStoreKind.File,
            SupportsEffectiveValues = true
        };

        public int BatchEnsureCount { get; private set; }

        public int DisposeCount { get; private set; }

        public int AsyncDisposeCount { get; private set; }

        public Task<ConfigurationEffectiveValueDocument> EnsureCreatedAsync(
            ConfigurationDefinition definition,
            string seedJson,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateDocument(definition, effectiveJson ?? seedJson));
        }

        public Task<IReadOnlyList<ConfigurationEffectiveValueDocument>> EnsureCreatedAsync(
            IReadOnlyList<ConfigurationEffectiveValueSeed> seeds,
            CancellationToken cancellationToken)
        {
            BatchEnsureCount++;
            if (failure is not null)
            {
                return Task.FromException<IReadOnlyList<ConfigurationEffectiveValueDocument>>(failure);
            }

            IReadOnlyList<ConfigurationEffectiveValueDocument> documents = seeds
                .Select(seed => CreateDocument(
                    seed.Definition,
                    effectiveJson ?? seed.MaterializeSeedJson()))
                .ToArray();
            return Task.FromResult(documents);
        }

        public Task<ConfigurationEffectiveValueDocument?> GetAsync(
            string definitionKey,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ConfigurationEffectiveValueDocument?>(null);
        }

        public Task<IReadOnlyList<ConfigurationEffectiveValueDocument?>> GetManyAsync(
            IReadOnlyList<string> definitionKeys,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ConfigurationEffectiveValueDocument?> documents = definitionKeys
                .Select(static _ => (ConfigurationEffectiveValueDocument?)null)
                .ToArray();
            return Task.FromResult(documents);
        }

        public Task<ConfigurationEffectiveValueDocument> SaveAsync(
            ConfigurationEffectiveValueSaveRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            DisposeCount++;
        }

        public ValueTask DisposeAsync()
        {
            AsyncDisposeCount++;
            return ValueTask.CompletedTask;
        }

        private static ConfigurationEffectiveValueDocument CreateDocument(
            ConfigurationDefinition definition,
            string json)
        {
            return new ConfigurationEffectiveValueDocument
            {
                DefinitionKey = definition.DefinitionKey,
                Json = json,
                Version = 1,
                SchemaVersion = definition.SchemaVersion,
                LastModifiedTime = DateTimeOffset.UtcNow
            };
        }
    }

    private sealed class TemporaryConfigurationDirectory : IDisposable
    {
        public TemporaryConfigurationDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"monica-configuration-input-plan-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public HostApplicationBuilder CreateHostBuilder()
        {
            return new HostApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = RootPath,
                DisableDefaults = true
            });
        }

        public Task WriteJsonAsync(string relativePath, string json)
        {
            return File.WriteAllTextAsync(
                Path.Combine(RootPath, relativePath),
                json,
                TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
