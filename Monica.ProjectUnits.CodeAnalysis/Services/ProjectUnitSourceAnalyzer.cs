using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Monica.ProjectUnits.CodeAnalysis.Abstractions;
using Monica.ProjectUnits.CodeAnalysis.Models;

namespace Monica.ProjectUnits.CodeAnalysis.Services;

internal sealed class ProjectUnitSourceAnalyzer : IProjectUnitSourceAnalyzer
{
    private static readonly Lock MSBUILD_REGISTRATION_LOCK = new();
    private readonly SemaphoreSlim _analysisLock = new(1, 1);
    private readonly ProjectUnitSymbolClassifier _classifier = new();

    public async Task<ProjectUnitSourceCatalog> AnalyzeAsync(
        ProjectUnitSourceAnalysisRequest request,
        IProgress<ProjectUnitSourceAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request.ProjectPaths);

        await _analysisLock.WaitAsync(cancellationToken);
        try
        {
            return await AnalyzeCoreAsync(request, progress, cancellationToken);
        }
        finally
        {
            _analysisLock.Release();
        }
    }

    private async Task<ProjectUnitSourceCatalog> AnalyzeCoreAsync(
        ProjectUnitSourceAnalysisRequest request,
        IProgress<ProjectUnitSourceAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(request.WorkspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Workspace root does not exist: {root}");
        }

        Report(progress, ProjectUnitSourceAnalysisStage.Initializing, 0, 0, null, "Preparing MSBuild analysis.");
        EnsureMsBuildRegistered();

        var diagnostics = new List<ProjectUnitSourceDiagnostic>();
        var projects = NormalizeProjectPaths(root, request.ProjectPaths, diagnostics);
        if (projects.Count == 0)
        {
            Report(progress, ProjectUnitSourceAnalysisStage.Completed, 0, 0, null, "No projects were selected.");
            return new ProjectUnitSourceCatalog(
                ProjectUnitSourceAnalysisContract.Version,
                0,
                0,
                diagnostics.Any(static diagnostic => diagnostic.Severity == ProjectUnitSourceDiagnosticSeverity.Error),
                DateTimeOffset.UtcNow,
                [],
                diagnostics);
        }

        using var workspace = MSBuildWorkspace.Create();
        var workspaceDiagnostics = new List<WorkspaceDiagnostic>();
        var workspaceDiagnosticsLock = new Lock();
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            lock (workspaceDiagnosticsLock)
            {
                workspaceDiagnostics.Add(args.Diagnostic);
            }
        });

        var loadedProjects = new List<Project>();
        for (var index = 0; index < projects.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectPath = projects[index];
            var relativePath = ToRelativePath(root, projectPath);
            Report(
                progress,
                ProjectUnitSourceAnalysisStage.LoadingProjects,
                index,
                projects.Count,
                relativePath,
                $"Loading {relativePath}.");

            if (!File.Exists(projectPath))
            {
                diagnostics.Add(new ProjectUnitSourceDiagnostic(
                    "ProjectUnit.Analysis.Project.Missing",
                    ProjectUnitSourceDiagnosticSeverity.Error,
                    "The selected project file does not exist.",
                    relativePath));
                continue;
            }

            try
            {
                var loaded = workspace.CurrentSolution.Projects.FirstOrDefault(project => string.Equals(
                                 project.FilePath,
                                 projectPath,
                                 StringComparison.OrdinalIgnoreCase))
                             ?? await workspace.OpenProjectAsync(
                                 projectPath,
                                 cancellationToken: cancellationToken);
                loadedProjects.Add(loaded);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(new ProjectUnitSourceDiagnostic(
                    "ProjectUnit.Analysis.Project.LoadFailed",
                    ProjectUnitSourceDiagnosticSeverity.Error,
                    $"MSBuild could not load the project: {exception.Message}",
                    relativePath));
            }
        }

        lock (workspaceDiagnosticsLock)
        {
            diagnostics.AddRange(workspaceDiagnostics.Select(diagnostic => new ProjectUnitSourceDiagnostic(
                diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                    ? "ProjectUnit.Analysis.MSBuild.Failure"
                    : "ProjectUnit.Analysis.MSBuild.Warning",
                diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                    ? ProjectUnitSourceDiagnosticSeverity.Warning
                    : ProjectUnitSourceDiagnosticSeverity.Information,
                diagnostic.Message)));
        }

        var analyzedProjects = 0;
        var candidates = new List<AnalyzedProjectUnitCandidate>();
        for (var index = 0; index < loadedProjects.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = loadedProjects[index];
            var projectPath = Path.GetFullPath(project.FilePath!);
            var relativeProjectPath = ToRelativePath(root, projectPath);
            Report(
                progress,
                ProjectUnitSourceAnalysisStage.AnalyzingProjects,
                index,
                loadedProjects.Count,
                relativeProjectPath,
                $"Analyzing {relativeProjectPath}.");

            if (project.Language != LanguageNames.CSharp)
            {
                diagnostics.Add(new ProjectUnitSourceDiagnostic(
                    "ProjectUnit.Analysis.Project.LanguageUnsupported",
                    ProjectUnitSourceDiagnosticSeverity.Error,
                    $"Only C# projects are supported; the project language is '{project.Language}'.",
                    relativeProjectPath));
                continue;
            }

            Compilation? compilation;
            try
            {
                compilation = await project.GetCompilationAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(new ProjectUnitSourceDiagnostic(
                    "ProjectUnit.Analysis.Project.CompilationFailed",
                    ProjectUnitSourceDiagnosticSeverity.Error,
                    $"Roslyn could not create the project compilation: {exception.Message}",
                    relativeProjectPath));
                continue;
            }

            if (compilation is null)
            {
                diagnostics.Add(new ProjectUnitSourceDiagnostic(
                    "ProjectUnit.Analysis.Project.CompilationMissing",
                    ProjectUnitSourceDiagnosticSeverity.Error,
                    "Roslyn did not produce a project compilation.",
                    relativeProjectPath));
                continue;
            }

            analyzedProjects++;
            var symbols = await GetDeclaredTypesAsync(project, cancellationToken);
            foreach (var symbol in symbols)
            {
                if (_classifier.Classify(symbol) is not { } candidate)
                {
                    continue;
                }

                var location = CreateLocation(root, candidate.Symbol);
                if (location is null)
                {
                    continue;
                }

                var attachedDiagnostics = candidate.Diagnostics
                    .Select(diagnostic => diagnostic with
                    {
                        ProjectPath = relativeProjectPath,
                        SourcePath = location.RelativePath,
                        Line = location.Line
                    })
                    .ToList();
                diagnostics.AddRange(attachedDiagnostics);
                candidates.Add(new AnalyzedProjectUnitCandidate(
                    relativeProjectPath,
                    project.Name,
                    compilation.AssemblyName ?? project.Name,
                    candidate,
                    location,
                    attachedDiagnostics));
            }
        }

        Report(
            progress,
            ProjectUnitSourceAnalysisStage.ResolvingDependencies,
            0,
            candidates.Count,
            null,
            "Resolving ProjectUnit dependencies.");
        var units = ResolveUnits(candidates, diagnostics);
        var isPartial = analyzedProjects != projects.Count
                        || diagnostics.Any(static diagnostic =>
                            diagnostic.Code is "ProjectUnit.Analysis.Project.Missing"
                                or "ProjectUnit.Analysis.Project.LoadFailed"
                                or "ProjectUnit.Analysis.Project.CompilationFailed"
                                or "ProjectUnit.Analysis.Project.CompilationMissing"
                                or "ProjectUnit.Analysis.Project.LanguageUnsupported");
        Report(
            progress,
            ProjectUnitSourceAnalysisStage.Completed,
            projects.Count,
            projects.Count,
            null,
            isPartial ? "ProjectUnit analysis completed with partial results." : "ProjectUnit analysis completed.");

        return new ProjectUnitSourceCatalog(
            ProjectUnitSourceAnalysisContract.Version,
            projects.Count,
            analyzedProjects,
            isPartial,
            DateTimeOffset.UtcNow,
            units,
            diagnostics
                .OrderByDescending(static diagnostic => diagnostic.Severity)
                .ThenBy(static diagnostic => diagnostic.ProjectPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static diagnostic => diagnostic.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static diagnostic => diagnostic.Line)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ToArray());
    }

    private static List<string> NormalizeProjectPaths(
        string root,
        IEnumerable<string> projectPaths,
        ICollection<ProjectUnitSourceDiagnostic> diagnostics)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in projectPaths)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(
                Path.IsPathRooted(value) ? value : Path.Combine(root, value));
            if (!IsWithinRoot(root, fullPath))
            {
                diagnostics.Add(new ProjectUnitSourceDiagnostic(
                    "ProjectUnit.Analysis.Project.OutsideWorkspace",
                    ProjectUnitSourceDiagnosticSeverity.Warning,
                    "External projects are excluded from workspace ProjectUnit analysis.",
                    fullPath.Replace('\\', '/')));
                continue;
            }

            if (seen.Add(fullPath))
            {
                results.Add(fullPath);
            }
        }

        return results.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<IReadOnlyList<INamedTypeSymbol>> GetDeclaredTypesAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        var results = new List<INamedTypeSymbol>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var document in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!document.SupportsSyntaxTree)
            {
                continue;
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (root is null || semanticModel is null)
            {
                continue;
            }

            foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is INamedTypeSymbol symbol
                    && seen.Add(symbol))
                {
                    results.Add(symbol);
                }
            }
        }

        return results;
    }

    private static ProjectUnitSourceLocation? CreateLocation(string root, INamedTypeSymbol symbol)
    {
        var syntax = symbol.DeclaringSyntaxReferences
            .OrderBy(reference => reference.SyntaxTree.FilePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (syntax is null || string.IsNullOrWhiteSpace(syntax.SyntaxTree.FilePath))
        {
            return null;
        }

        var span = syntax.SyntaxTree.GetLineSpan(syntax.Span);
        return new ProjectUnitSourceLocation(
            ToRelativePath(root, syntax.SyntaxTree.FilePath),
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1);
    }

    private static IReadOnlyList<ProjectUnitSourceUnit> ResolveUnits(
        IReadOnlyList<AnalyzedProjectUnitCandidate> candidates,
        ICollection<ProjectUnitSourceDiagnostic> allDiagnostics)
    {
        var byIdentity = new Dictionary<string, AnalyzedProjectUnitCandidate>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var identity = SymbolIdentity.Create(candidate.Candidate.Symbol);
            if (!byIdentity.TryAdd(identity, candidate))
            {
                allDiagnostics.Add(new ProjectUnitSourceDiagnostic(
                    "ProjectUnit.Analysis.Unit.IdentityDuplicate",
                    ProjectUnitSourceDiagnosticSeverity.Error,
                    $"Multiple source declarations resolve to the same assembly/type identity '{identity}'.",
                    candidate.ProjectPath,
                    candidate.Location.RelativePath,
                    candidate.Location.Line));
            }
        }

        var dependenciesByKey = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var diagnosticsByKey = new Dictionary<string, IReadOnlyList<ProjectUnitSourceDiagnostic>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var key = CreateCatalogKey(candidate.ProjectPath, candidate.Candidate.Symbol);
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            var unitDiagnostics = candidate.Diagnostics.ToList();
            foreach (var dependency in candidate.Candidate.Dependencies)
            {
                if (byIdentity.TryGetValue(SymbolIdentity.Create(dependency.Symbol), out var resolved))
                {
                    var dependencyKey = CreateCatalogKey(resolved.ProjectPath, resolved.Candidate.Symbol);
                    if (!string.Equals(dependencyKey, key, StringComparison.Ordinal))
                    {
                        dependencies.Add(dependencyKey);
                    }
                }
                else if (dependency.MissingDiagnosticCode is not null)
                {
                    var diagnostic = new ProjectUnitSourceDiagnostic(
                        dependency.MissingDiagnosticCode,
                        ProjectUnitSourceDiagnosticSeverity.Warning,
                        $"Associated ProjectUnit '{dependency.Symbol.GetRuntimeName()}' was not discovered in the analyzed workspace.",
                        candidate.ProjectPath,
                        candidate.Location.RelativePath,
                        candidate.Location.Line);
                    unitDiagnostics.Add(diagnostic);
                    allDiagnostics.Add(diagnostic);
                }
            }

            dependenciesByKey[key] = dependencies.Order(StringComparer.Ordinal).ToArray();
            diagnosticsByKey[key] = unitDiagnostics;
        }

        var dependedBy = candidates
            .Select(candidate => CreateCatalogKey(candidate.ProjectPath, candidate.Candidate.Symbol))
            .ToDictionary(static key => key, static _ => new List<string>(), StringComparer.Ordinal);
        foreach (var (source, dependencies) in dependenciesByKey)
        {
            foreach (var dependency in dependencies)
            {
                if (dependedBy.TryGetValue(dependency, out var dependents))
                {
                    dependents.Add(source);
                }
            }
        }

        return candidates
            .Select(candidate =>
            {
                var symbol = candidate.Candidate.Symbol;
                var key = CreateCatalogKey(candidate.ProjectPath, symbol);
                return new ProjectUnitSourceUnit(
                    key,
                    symbol.GetRuntimeName(),
                    candidate.ProjectPath,
                    candidate.ProjectName,
                    candidate.AssemblyName,
                    symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                    symbol.Name,
                    candidate.Candidate.UnitType,
                    candidate.Candidate.Title,
                    candidate.Candidate.Description,
                    candidate.Candidate.Owner,
                    candidate.Candidate.Tags,
                    candidate.Candidate.RequirementIds,
                    candidate.Candidate.HasExplicitMetadata,
                    candidate.Location,
                    dependenciesByKey[key],
                    dependedBy[key].Order(StringComparer.Ordinal).ToArray(),
                    diagnosticsByKey[key]);
            })
            .OrderBy(static unit => unit.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static unit => unit.UnitType)
            .ThenBy(static unit => unit.RuntimeKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CreateCatalogKey(string projectPath, INamedTypeSymbol symbol)
        => $"{projectPath}::{symbol.GetRuntimeName()}";

    private static bool IsWithinRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative)
               && relative != ".."
               && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string ToRelativePath(string root, string path)
        => Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/');

    private static void EnsureMsBuildRegistered()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (MSBUILD_REGISTRATION_LOCK)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }

    private static void Report(
        IProgress<ProjectUnitSourceAnalysisProgress>? progress,
        ProjectUnitSourceAnalysisStage stage,
        int completed,
        int total,
        string? currentProject,
        string message)
    {
        progress?.Report(new ProjectUnitSourceAnalysisProgress(
            stage,
            completed,
            total,
            CalculatePercentage(stage, completed, total),
            currentProject,
            message));
    }

    private static decimal? CalculatePercentage(
        ProjectUnitSourceAnalysisStage stage,
        int completed,
        int total)
    {
        var ratio = total == 0 ? 0m : Math.Clamp((decimal)completed / total, 0m, 1m);
        return stage switch
        {
            ProjectUnitSourceAnalysisStage.Initializing => 0m,
            ProjectUnitSourceAnalysisStage.LoadingProjects => 5m + 35m * ratio,
            ProjectUnitSourceAnalysisStage.AnalyzingProjects => 40m + 50m * ratio,
            ProjectUnitSourceAnalysisStage.ResolvingDependencies => 95m,
            ProjectUnitSourceAnalysisStage.Completed => 100m,
            _ => null
        };
    }

    private sealed record AnalyzedProjectUnitCandidate(
        string ProjectPath,
        string ProjectName,
        string AssemblyName,
        ProjectUnitSymbolCandidate Candidate,
        ProjectUnitSourceLocation Location,
        IReadOnlyList<ProjectUnitSourceDiagnostic> Diagnostics);
}
