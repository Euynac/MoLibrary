using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Configuration.Annotations;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Events;
using Monica.JobScheduler.Abstractions;
using Monica.ProjectUnits.Annotations;
using Monica.ProjectUnits.CodeAnalysis.Models;
using Monica.ProjectUnits.CodeAnalysis.Services;
using Monica.ProjectUnits.Models;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.WebApi.Abstractions;
using Monica.WebApi.AutoControllers.Abstractions;
using Xunit;

namespace Test.Monica.ProjectUnits;

public sealed class ProjectUnitCodeAnalysisTests
{
    [Fact]
    public void Classifier_matches_runtime_precedence_and_custom_base_types()
    {
        var compilation = CreateCompilation(CLASSIFICATION_SOURCE);
        var classifier = new ProjectUnitSymbolClassifier();

        var actual = EXPECTED_TYPES.ToDictionary(
            pair => pair.Key,
            pair => classifier.Classify(GetType(compilation, pair.Key))?.UnitType);

        actual.Should().BeEquivalentTo(EXPECTED_TYPES);
        classifier.Classify(GetType(compilation, "Samples.AbstractDomainService")).Should().BeNull();
        classifier.Classify(GetType(compilation, "Samples.GenericDomainService`1")).Should().BeNull();
    }

    [Fact]
    public void Classifier_normalizes_metadata_requirements_and_dependencies()
    {
        var compilation = CreateCompilation(CLASSIFICATION_SOURCE);
        var classifier = new ProjectUnitSymbolClassifier();

        var candidate = classifier.Classify(GetType(compilation, "Samples.CommandCustom"));

        candidate.Should().NotBeNull();
        candidate!.Title.Should().Be("Custom command");
        candidate.Description.Should().Be("Coordinates a custom command.");
        candidate.Owner.Should().Be("Flight Team");
        candidate.Tags.Should().Equal("flight", "command");
        candidate.RequirementIds.Should().Equal("FIPS-FLIGHT-001", "FIPS-FLIGHT-002");
        candidate.HasExplicitMetadata.Should().BeTrue();
        candidate.Dependencies.Select(dependency => dependency.Symbol.Name).Should().Contain(
            "SampleTypedRequest",
            "SampleDomainService");
    }

    [Fact]
    public async Task Analyzer_returns_stable_partial_catalog_and_reports_progress()
    {
        using var fixture = new TemporaryProjectFixture();
        var progress = new ProgressRecorder();
        var analyzer = new ProjectUnitSourceAnalyzer();

        var result = await analyzer.AnalyzeAsync(
            new ProjectUnitSourceAnalysisRequest(
                fixture.Root,
                [fixture.ProjectPath, Path.Combine(fixture.Root, "Missing.csproj")]),
            progress,
            TestContext.Current.CancellationToken);

        result.RequestedProjectCount.Should().Be(2);
        result.AnalyzedProjectCount.Should().Be(1);
        result.IsPartial.Should().BeTrue();
        result.Units.Should().ContainSingle();
        result.Units[0].Should().Match<ProjectUnitSourceUnit>(unit =>
            unit.CatalogKey == "Sample.csproj::Sample.ManagedUnit"
            && unit.RuntimeKey == "Sample.ManagedUnit"
            && unit.UnitType == EProjectUnitType.DomainService
            && unit.Title == "Managed unit"
            && unit.Description == "Coordinates the sample workflow."
            && unit.Owner == "Sample Team"
            && unit.Source.RelativePath == "ManagedUnit.cs"
            && unit.Source.Line > 0);
        result.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "ProjectUnit.Analysis.Project.Missing"
            && diagnostic.Severity == ProjectUnitSourceDiagnosticSeverity.Error);
        progress.Items.Should().Contain(item => item.Stage == ProjectUnitSourceAnalysisStage.LoadingProjects);
        progress.Items.Should().Contain(item => item.Stage == ProjectUnitSourceAnalysisStage.AnalyzingProjects);
        progress.Items.Last().Should().Match<ProjectUnitSourceAnalysisProgress>(item =>
            item.Stage == ProjectUnitSourceAnalysisStage.Completed && item.Percentage == 100m);
    }

    private static readonly IReadOnlyDictionary<string, EProjectUnitType?> EXPECTED_TYPES =
        new Dictionary<string, EProjectUnitType?>
        {
            ["Samples.SampleCrudService"] = EProjectUnitType.CrudApplicationService,
            ["Samples.CommandCustom"] = EProjectUnitType.ApplicationService,
            ["Samples.SampleOptions"] = EProjectUnitType.Configuration,
            ["Samples.SampleDistributedHandler"] = EProjectUnitType.DomainEventHandler,
            ["Samples.SampleLocalHandler"] = EProjectUnitType.LocalEventHandler,
            ["Samples.SampleEvent"] = EProjectUnitType.DomainEvent,
            ["Samples.RepositorySampleEntity"] = EProjectUnitType.Repository,
            ["Samples.SampleEntity"] = EProjectUnitType.Entity,
            ["Samples.SampleDomainService"] = EProjectUnitType.DomainService,
            ["Samples.SampleRecurringJob"] = EProjectUnitType.RecurringJob,
            ["Samples.SampleTriggeredJob"] = EProjectUnitType.TriggeredJob,
            ["Samples.SampleRequest"] = EProjectUnitType.RequestDto
        };

    private const string CLASSIFICATION_SOURCE = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.Logging.Abstractions;
        using Monica.Configuration.Annotations;
        using Monica.EventBus.Abstractions.Handlers;
        using Monica.EventBus.Events;
        using Monica.JobScheduler.Abstractions;
        using Monica.ProjectUnits.Annotations;
        using Monica.Repository.Entity.Abstractions;
        using Monica.Repository.Persistence.Abstractions;
        using Monica.WebApi.Abstractions;
        using Monica.WebApi.AutoControllers.Abstractions;

        namespace Samples;

        public sealed class SampleRequest : IResultRequest { }
        public sealed class SampleTypedRequest : IResultRequest<string> { }

        public abstract class OurApplicationService<TRequest, TResponse> : ApplicationService<TRequest, TResponse>
            where TRequest : IResultRequest<TResponse> { }

        [ProjectUnitMetadata("  Custom command  ", Owner = " Flight Team ",
            Description = " Coordinates a custom command. ", Tags = [" flight ", "FLIGHT", "command"])]
        [ProjectUnitRequirement(" FIPS-FLIGHT-001 ")]
        [ProjectUnitRequirement("fips-flight-001")]
        [ProjectUnitRequirement("FIPS-FLIGHT-002")]
        public sealed class CommandCustom(SampleDomainService service)
            : OurApplicationService<SampleTypedRequest, string>
        {
            public override Task<Monica.Core.Results.Res<string>> Handle(
                SampleTypedRequest request,
                CancellationToken cancellationToken) => throw new NotImplementedException();
        }

        public sealed class SampleCrudService : ApplicationService, ICrudApplicationService { }
        [Configuration(DisplayName = "Options")] public sealed class SampleOptions { }
        public sealed class SampleEvent : DomainEvent { }
        public sealed class SampleDistributedHandler : IDistributedEventHandler<SampleEvent> { }
        public sealed class SampleLocalHandler : ILocalEventHandler<SampleEvent> { }
        public sealed class SampleEntity : IEntity { }
        public interface IRepositorySampleEntity : IRepository<SampleEntity> { }
        public sealed class RepositorySampleEntity : IRepositorySampleEntity { }
        public sealed class SampleDomainService : DomainService { }
        public abstract class AbstractDomainService : DomainService { }
        public sealed class GenericDomainService<T> : DomainService { }
        public sealed class SampleRecurringJob : RecurringJob
        {
            public SampleRecurringJob() : base(NullLogger.Instance) { }
            public override Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
        public sealed class SampleTriggeredJob : TriggeredJob<SampleRequest>
        {
            public SampleTriggeredJob() : base(NullLogger.Instance) { }
            public override Task ExecuteAsync(SampleRequest parameters, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
        """;

    private static CSharpCompilation CreateCompilation(string source)
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var projectAssemblies = new[]
        {
            typeof(ApplicationService).Assembly.Location,
            typeof(ProjectUnitMetadataAttribute).Assembly.Location,
            typeof(ConfigurationAttribute).Assembly.Location,
            typeof(IDomainEvent).Assembly.Location,
            typeof(RecurringJob).Assembly.Location,
            typeof(IEntity).Assembly.Location,
            typeof(IRepository<>).Assembly.Location,
            typeof(ICrudApplicationService).Assembly.Location,
            typeof(IDistributedEventHandler<>).Assembly.Location,
            typeof(NullLogger).Assembly.Location
        };
        var references = trustedAssemblies.Concat(projectAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "ProjectUnitCodeAnalysisTests",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static INamedTypeSymbol GetType(Compilation compilation, string metadataName)
        => compilation.GetTypeByMetadataName(metadataName)
           ?? throw new InvalidOperationException($"Test type '{metadataName}' was not compiled.");

    private sealed class ProgressRecorder : IProgress<ProjectUnitSourceAnalysisProgress>
    {
        public List<ProjectUnitSourceAnalysisProgress> Items { get; } = [];

        public void Report(ProjectUnitSourceAnalysisProgress value) => Items.Add(value);
    }

    private sealed class TemporaryProjectFixture : IDisposable
    {
        public TemporaryProjectFixture()
        {
            Root = Directory.CreateTempSubdirectory("monica-project-unit-analysis-").FullName;
            ProjectPath = Path.Combine(Root, "Sample.csproj");
            File.WriteAllText(ProjectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(Root, "ManagedUnit.cs"), """
                namespace Monica.WebApi.Abstractions
                {
                    public abstract class DomainService { }
                }

                namespace Monica.ProjectUnits.Annotations
                {
                    [System.AttributeUsage(System.AttributeTargets.Class)]
                    public sealed class ProjectUnitMetadataAttribute(string title) : System.Attribute
                    {
                        public string Title { get; } = title;
                        public string? Owner { get; set; }
                        public string? Description { get; set; }
                        public string[] Tags { get; set; } = [];
                    }
                }

                namespace Sample
                {
                    using Monica.ProjectUnits.Annotations;
                    using Monica.WebApi.Abstractions;

                    /// <summary>Fallback documentation.</summary>
                    [ProjectUnitMetadata("Managed unit", Owner = "Sample Team",
                        Description = "Coordinates the sample workflow.")]
                    public sealed class ManagedUnit : DomainService { }
                }
                """);
        }

        public string Root { get; }

        public string ProjectPath { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // MSBuild may briefly retain a file handle after workspace disposal on Windows.
            }
            catch (UnauthorizedAccessException)
            {
                // Temporary test cleanup must not hide the assertion result.
            }
        }
    }
}
