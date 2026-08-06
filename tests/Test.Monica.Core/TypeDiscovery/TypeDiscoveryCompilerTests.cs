using AwesomeAssertions;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.TypeDiscovery.Models;
using Monica.Core.TypeDiscovery.Services;
using Xunit;

namespace Test.Monica.Core.TypeDiscovery;

public sealed class TypeDiscoveryCompilerTests
{
    [Fact]
    public void Compile_WhenInputsRepeatAndQueriesOverlap_ShouldDeduplicateTypesAndReuseShapes()
    {
        var query = TypeQuery.ClosedClass.AssignableTo<IDiscoveryMarker>();
        var equivalentQuery = TypeQuery.AllOf(
            TypeQuery.All,
            TypeQuery.ClosedClass,
            TypeQuery.All.AssignableTo<IDiscoveryMarker>(),
            TypeQuery.All.AssignableTo<IDiscoveryMarker>());
        var differentQuery = TypeQuery.ClosedClass.HasAttribute<InheritedDiscoveryAttribute>(inherit: true);
        var plan = CreatePlan(query, equivalentQuery, differentQuery);

        var compilation = TypeDiscoveryCompiler.Compile(
            [typeof(AttributedGenericCandidate), typeof(AttributedGenericCandidate)],
            [(ITypeDiscoveryPlan)plan]);

        var matches = compilation.GetMatches(query);
        var equivalentMatches = compilation.GetMatches(equivalentQuery);
        var differentMatches = compilation.GetMatches(differentQuery);

        matches.Should().BeSameAs(equivalentMatches);
        matches.Should().ContainSingle().Which.Type.Should().Be(typeof(AttributedGenericCandidate));
        differentMatches.Should().ContainSingle();
        differentMatches[0].Shape.Should().BeSameAs(matches[0].Shape);
    }

    [Fact]
    public void Compile_WhenQueryUsesInheritedAttributeAndOpenGenericInterface_ShouldCaptureMatchAndApplyGlobalExclusion()
    {
        var inheritedGenericQuery = TypeQuery.ClosedClass
            .HasAttribute<InheritedDiscoveryAttribute>(inherit: true)
            .ImplementsOpenGeneric(typeof(IDiscoveryContract<>));
        var directlyAttributedQuery = TypeQuery.ClosedClass
            .HasAttribute<InheritedDiscoveryAttribute>(inherit: false);
        var plan = CreatePlan(inheritedGenericQuery, directlyAttributedQuery, TypeQuery.All);

        var compilation = TypeDiscoveryCompiler.Compile(
            [typeof(AttributedGenericCandidate), typeof(ExcludedAttributedGenericCandidate)],
            [(ITypeDiscoveryPlan)plan]);

        var match = compilation.GetMatches(inheritedGenericQuery).Should().ContainSingle().Which;
        var openGenericMatch = match.OpenGenericInterfaces.Should().ContainSingle().Which;

        match.Type.Should().Be(typeof(AttributedGenericCandidate));
        openGenericMatch.ImplementationType.Should().Be(typeof(AttributedGenericCandidate));
        openGenericMatch.ClosedInterface.Should().Be(typeof(IDiscoveryContract<string>));
        openGenericMatch.GenericArguments.Should().Equal(typeof(string));
        compilation.GetMatches(directlyAttributedQuery).Should().BeEmpty();
        compilation.GetMatches(TypeQuery.All)
            .Should().ContainSingle()
            .Which.Shape.Should().BeSameAs(match.Shape);
    }

    private static TypeDiscoveryPlan<DiscoveryOptions> CreatePlan(params TypeQuery[] queries)
    {
        var plan = new TypeDiscoveryPlan<DiscoveryOptions>();
        foreach (var query in queries)
        {
            plan.Match(query, static (_, _) => { });
        }

        return plan;
    }

    private sealed class DiscoveryOptions : IModuleOptions;
}

internal interface IDiscoveryMarker;

internal interface IDiscoveryContract<T>;

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
internal sealed class InheritedDiscoveryAttribute : Attribute;

[InheritedDiscovery]
internal abstract class AttributedDiscoveryBase;

internal sealed class AttributedGenericCandidate :
    AttributedDiscoveryBase,
    IDiscoveryMarker,
    IDiscoveryContract<string>;

[ExcludeFromBusinessTypeDiscovery]
internal sealed class ExcludedAttributedGenericCandidate :
    AttributedDiscoveryBase,
    IDiscoveryMarker,
    IDiscoveryContract<string>;
