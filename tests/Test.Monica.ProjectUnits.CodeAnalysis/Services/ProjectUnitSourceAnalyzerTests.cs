using AwesomeAssertions;
using Monica.ProjectUnits.CodeAnalysis.Models;
using Monica.ProjectUnits.CodeAnalysis.Services;
using Xunit;

namespace Test.Monica.ProjectUnits.CodeAnalysis.Services;

public sealed class ProjectUnitSourceAnalyzerTests
{
    [Fact]
    public void Constructor_ShouldBePublicForWorkflowComposition()
    {
        typeof(ProjectUnitSourceAnalyzer).IsPublic.Should().BeTrue();
        new ProjectUnitSourceAnalyzer().Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenASelectedProjectIsMissing_ShouldReturnStablePartialCatalog()
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

        result.ContractVersion.Should().Be("monica-project-units-source/v2");
        result.RequestedProjectCount.Should().Be(2);
        result.AnalyzedProjectCount.Should().Be(1);
        result.IsPartial.Should().BeTrue();
        result.Units.Should().ContainSingle();
        result.Units[0].Should().Match<ProjectUnitSourceUnit>(unit =>
            unit.CatalogKey == "Sample.csproj::Sample.ManagedUnit"
            && unit.RuntimeKey == "Sample.ManagedUnit"
            && unit.UnitType == ProjectUnitSourceType.DomainService
            && unit.Title == "Managed unit"
            && unit.Description == "Coordinates the sample workflow."
            && unit.Owner == "Sample Team"
            && unit.ExecutionPoints.Count == 0
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
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // MSBuild may briefly retain a file handle after workspace disposal on Windows.
            }
        }
    }
}
