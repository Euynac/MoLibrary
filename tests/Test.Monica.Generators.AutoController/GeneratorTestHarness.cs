using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Monica.Core.Results;
using Monica.DependencyInjection.Abstractions;
using Monica.WebApi.Abstractions;
using Monica.WebApi.Annotations;

namespace Test.Monica.Generators.AutoController;

internal static class GeneratorTestHarness
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);
    private static readonly ImmutableArray<MetadataReference> MetadataReferences = CreateMetadataReferences();

    public static GeneratorTestRun Run(
        string source,
        params IIncrementalGenerator[] generators)
    {
        return Run([source], optionsProvider: null, generators);
    }

    public static GeneratorTestRun Run(
        IReadOnlyList<string> sources,
        params IIncrementalGenerator[] generators)
    {
        return Run(sources, optionsProvider: null, generators);
    }

    public static GeneratorTestRun RunWithProjectDirectory(
        string source,
        string projectDirectory,
        params IIncrementalGenerator[] generators)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.MSBuildProjectDirectory"] = projectDirectory,
                ["build_property.ProjectDir"] = projectDirectory
            });

        return Run([source], optionsProvider, generators);
    }

    private static GeneratorTestRun Run(
        IReadOnlyList<string> sources,
        AnalyzerConfigOptionsProvider? optionsProvider,
        params IIncrementalGenerator[] generators)
    {
        var syntaxTrees = sources
            .Select((source, index) => CSharpSyntaxTree.ParseText(
                source,
                ParseOptions,
                path: $"Source{index}.cs"))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            assemblyName: $"GeneratorScenario_{Guid.NewGuid():N}",
            syntaxTrees,
            MetadataReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators.Select(static generator => generator.AsSourceGenerator()),
            additionalTexts: null,
            parseOptions: ParseOptions,
            optionsProvider: optionsProvider);

        return Run(compilation, driver);
    }

    private sealed class TestAnalyzerConfigOptionsProvider(
        IReadOnlyDictionary<string, string> globalValues)
        : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions EmptyOptions = new TestAnalyzerConfigOptions(
            new Dictionary<string, string>());
        private readonly AnalyzerConfigOptions _globalOptions = new TestAnalyzerConfigOptions(globalValues);

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return EmptyOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return EmptyOptions;
        }
    }

    private sealed class TestAnalyzerConfigOptions(
        IReadOnlyDictionary<string, string> values)
        : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            return values.TryGetValue(key, out value!);
        }
    }

    private static GeneratorTestRun Run(CSharpCompilation inputCompilation, GeneratorDriver driver)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(
            inputCompilation,
            out var outputCompilation,
            out var driverDiagnostics);

        return new GeneratorTestRun(
            inputCompilation,
            (CSharpCompilation)outputCompilation,
            driver,
            driver.GetRunResult(),
            driverDiagnostics);
    }

    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var assemblyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                assemblyPaths.Add(path);
            }
        }

        AddAssemblyClosure(typeof(Res).Assembly, assemblyPaths);
        AddAssemblyClosure(typeof(ICachedServiceProvider).Assembly, assemblyPaths);
        AddAssemblyClosure(typeof(ApplicationService).Assembly, assemblyPaths);
        AddAssemblyClosure(typeof(ApiEndpointAttribute).Assembly, assemblyPaths);

        return assemblyPaths
            .Where(File.Exists)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static void AddAssemblyClosure(Assembly rootAssembly, ISet<string> assemblyPaths)
    {
        var pending = new Queue<Assembly>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue(rootAssembly);

        while (pending.TryDequeue(out var assembly))
        {
            if (!visited.Add(assembly.FullName ?? assembly.GetName().Name ?? string.Empty))
            {
                continue;
            }

            if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            {
                assemblyPaths.Add(assembly.Location);
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                try
                {
                    pending.Enqueue(Assembly.Load(reference));
                }
                catch (FileNotFoundException)
                {
                    // Optional runtime dependencies that are irrelevant to the in-memory scenario may be absent.
                }
            }
        }
    }

    internal sealed record GeneratorTestRun(
        CSharpCompilation InputCompilation,
        CSharpCompilation OutputCompilation,
        GeneratorDriver Driver,
        GeneratorDriverRunResult RunResult,
        ImmutableArray<Diagnostic> DriverDiagnostics)
    {
        public IReadOnlyDictionary<string, string> GeneratedSources => RunResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .ToDictionary(
                static source => source.HintName,
                static source => source.SourceText.ToString(),
                StringComparer.Ordinal);

        public ImmutableArray<Diagnostic> OutputErrors => OutputCompilation
            .GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        public GeneratorTestRun RunAgain()
        {
            return Run(InputCompilation, Driver);
        }
    }
}
