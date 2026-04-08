using System.Reflection;
using AwesomeAssertions;
using Monica.Core.TypeDiscovery.Services.Support;
using Xunit;

namespace Test.Monica.Core.TypeDiscovery;

public class TypeFinderAssemblyPlanBuilderTests
{
    [Fact]
    public void ResolveDefaultProjectAssemblies_ShouldIncludeTransitiveProjectDependenciesOnly()
    {
        var entryAssembly = typeof(TypeFinderAssemblyPlanBuilderTests).Assembly;
        var dependencyLibraries = new[]
        {
            CreateProjectLibrary(
                libraryName: entryAssembly.GetName().Name!,
                assemblyName: entryAssembly.GetName().Name!,
                dependencies: ["Monica.Docs.Shared.Platform.Infrastructure", "Monica.Core"]),
            CreateProjectLibrary(
                libraryName: "Monica.Docs.Shared.Platform.Infrastructure",
                assemblyName: "Monica.Docs.Shared.Platform.Infrastructure",
                dependencies: ["Monica.Docs.Domains.Documentation.Application", "Monica.Markdown"]),
            CreateProjectLibrary(
                libraryName: "Monica.Core",
                assemblyName: "Monica.Core"),
            CreateProjectLibrary(
                libraryName: "Monica.Docs.Domains.Documentation.Application",
                assemblyName: "Monica.Docs.Domains.Documentation.Application",
                dependencies: ["Monica.Docs.Domains.Documentation.Domain", "Monica.Docs.Shared.Platform.Protocol"]),
            CreateProjectLibrary(
                libraryName: "Monica.Docs.Domains.Documentation.Domain",
                assemblyName: "Monica.Docs.Domains.Documentation.Domain"),
            CreateProjectLibrary(
                libraryName: "Monica.Docs.Shared.Platform.Protocol",
                assemblyName: "Monica.Docs.Shared.Platform.Protocol"),
            CreatePackageLibrary(
                libraryName: "Monica.Markdown",
                assemblyName: "Monica.Markdown"),
            CreatePackageLibrary(
                libraryName: "Newtonsoft.Json",
                assemblyName: "Newtonsoft.Json")
        };

        var referencedAssemblies = new[]
        {
            new AssemblyName("Monica.Docs.Shared.Platform.Infrastructure"),
            new AssemblyName("Monica.Core")
        };

        var resolvedAssemblies = TypeFinderAssemblyPlanBuilder.ResolveDefaultProjectAssemblies(
            entryAssembly,
            dependencyLibraries,
            referencedAssemblies);

        resolvedAssemblies
            .Select(static assembly => assembly.Name)
            .Should()
            .BeEquivalentTo(
                "Monica.Core",
                "Monica.Docs.Shared.Platform.Infrastructure",
                "Monica.Docs.Domains.Documentation.Application",
                "Monica.Docs.Domains.Documentation.Domain",
                "Monica.Docs.Shared.Platform.Protocol");
    }

    [Fact]
    public void ResolveDefaultProjectAssemblies_WhenEntryProjectLibraryIsMissing_ShouldFallBackToDirectProjectReferences()
    {
        var entryAssembly = typeof(TypeFinderAssemblyPlanBuilderTests).Assembly;
        var dependencyLibraries = new[]
        {
            CreateProjectLibrary(
                libraryName: "Monica.Docs.Shared.Platform.Infrastructure",
                assemblyName: "Monica.Docs.Shared.Platform.Infrastructure",
                dependencies: ["Monica.Docs.Domains.Documentation.Application"]),
            CreateProjectLibrary(
                libraryName: "Monica.Docs.Domains.Documentation.Application",
                assemblyName: "Monica.Docs.Domains.Documentation.Application"),
            CreatePackageLibrary(
                libraryName: "Monica.Markdown",
                assemblyName: "Monica.Markdown")
        };

        var referencedAssemblies = new[]
        {
            new AssemblyName("Monica.Docs.Shared.Platform.Infrastructure"),
            new AssemblyName("Monica.Markdown")
        };

        var resolvedAssemblies = TypeFinderAssemblyPlanBuilder.ResolveDefaultProjectAssemblies(
            entryAssembly,
            dependencyLibraries,
            referencedAssemblies);

        resolvedAssemblies
            .Select(static assembly => assembly.Name)
            .Should()
            .BeEquivalentTo(
                "Monica.Docs.Shared.Platform.Infrastructure",
                "Monica.Docs.Domains.Documentation.Application");
    }

    [Fact]
    public void ResolveDefaultProjectAssemblies_WhenProjectDependencyEdgesAreMissing_ShouldFallBackToDirectProjectReferencesOnly()
    {
        var entryAssembly = typeof(TypeFinderAssemblyPlanBuilderTests).Assembly;
        var dependencyLibraries = new[]
        {
            CreateProjectLibrary(
                libraryName: entryAssembly.GetName().Name!,
                assemblyName: entryAssembly.GetName().Name!),
            CreateProjectLibrary(
                libraryName: "Monica.Docs.Shared.Platform.Infrastructure",
                assemblyName: "Monica.Docs.Shared.Platform.Infrastructure"),
            CreatePackageLibrary(
                libraryName: "Monica.Markdown",
                assemblyName: "Monica.Markdown")
        };

        var referencedAssemblies = new[]
        {
            new AssemblyName("Monica.Docs.Shared.Platform.Infrastructure"),
            new AssemblyName("Monica.Markdown")
        };

        var resolvedAssemblies = TypeFinderAssemblyPlanBuilder.ResolveDefaultProjectAssemblies(
            entryAssembly,
            dependencyLibraries,
            referencedAssemblies);

        resolvedAssemblies
            .Select(static assembly => assembly.Name)
            .Should()
            .BeEquivalentTo("Monica.Docs.Shared.Platform.Infrastructure");
    }

    [Fact]
    public void ResolveDefaultProjectAssemblies_WhenNoProjectLibrariesExist_ShouldFallBackToAllDirectReferences()
    {
        var entryAssembly = typeof(TypeFinderAssemblyPlanBuilderTests).Assembly;
        var dependencyLibraries = new[]
        {
            CreatePackageLibrary(
                libraryName: "Monica.Markdown",
                assemblyName: "Monica.Markdown"),
            CreatePackageLibrary(
                libraryName: "Newtonsoft.Json",
                assemblyName: "Newtonsoft.Json")
        };

        var referencedAssemblies = new[]
        {
            new AssemblyName("Monica.Markdown"),
            new AssemblyName("Newtonsoft.Json")
        };

        var resolvedAssemblies = TypeFinderAssemblyPlanBuilder.ResolveDefaultProjectAssemblies(
            entryAssembly,
            dependencyLibraries,
            referencedAssemblies);

        resolvedAssemblies
            .Select(static assembly => assembly.Name)
            .Should()
            .BeEquivalentTo("Monica.Markdown", "Newtonsoft.Json");
    }

    private static TypeFinderDependencyLibraryDescriptor CreateProjectLibrary(
        string libraryName,
        string assemblyName,
        IReadOnlyList<string>? dependencies = null)
    {
        return new TypeFinderDependencyLibraryDescriptor(
            libraryName,
            "project",
            "1.0.0",
            new AssemblyName(assemblyName),
            dependencies ?? []);
    }

    private static TypeFinderDependencyLibraryDescriptor CreatePackageLibrary(
        string libraryName,
        string assemblyName,
        IReadOnlyList<string>? dependencies = null)
    {
        return new TypeFinderDependencyLibraryDescriptor(
            libraryName,
            "package",
            "1.0.0",
            new AssemblyName(assemblyName),
            dependencies ?? []);
    }
}
