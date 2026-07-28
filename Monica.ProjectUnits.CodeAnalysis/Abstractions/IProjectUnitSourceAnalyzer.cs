using Monica.ProjectUnits.CodeAnalysis.Models;

namespace Monica.ProjectUnits.CodeAnalysis.Abstractions;

/// <summary>
/// Analyzes C# projects semantically and returns a source-level Monica ProjectUnit catalog without loading application
/// assemblies or starting application hosts.
/// </summary>
public interface IProjectUnitSourceAnalyzer
{
    /// <summary>
    /// Analyzes the requested projects and reports deterministic project-loading and semantic-analysis progress.
    /// </summary>
    /// <param name="request">Workspace root and normalized project files to analyze.</param>
    /// <param name="progress">Optional progress observer. Callbacks may occur on background threads.</param>
    /// <param name="cancellationToken">Cancellation token for project loading and semantic analysis.</param>
    /// <returns>A serializable source catalog. Project failures are represented as diagnostics and partial results.</returns>
    Task<ProjectUnitSourceCatalog> AnalyzeAsync(
        ProjectUnitSourceAnalysisRequest request,
        IProgress<ProjectUnitSourceAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
