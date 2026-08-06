using AwesomeAssertions;
using System.Reflection;
using System.Reflection.Emit;
using Monica.Core.Modularity.Models;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleKeyTests
{
    [Fact]
    public void FromType_WhenModuleTypeIsProvided_ShouldUseItsStableFullName()
    {
        var key = ModuleKey.FromModuleType(typeof(DiagnosticModule));

        key.Value.Should().Be(typeof(DiagnosticModule).FullName);
    }

    [Fact]
    public void Equality_WhenDerivedFromTheSameType_ShouldRemainStableForDiagnosticLookups()
    {
        var first = ModuleKey.FromModuleType(typeof(DiagnosticModule));
        var second = ModuleKey.FromModuleType(typeof(DiagnosticModule));
        var modules = new Dictionary<ModuleKey, string>
        {
            [first] = "registered"
        };

        second.Should().Be(first);
        (second == first).Should().BeTrue();
        second.GetHashCode().Should().Be(first.GetHashCode());
        modules.Should().ContainKey(second);
    }

    [Fact]
    public void Equality_WhenDerivedFromDifferentTypes_ShouldRemainDistinct()
    {
        var first = ModuleKey.FromModuleType(typeof(DiagnosticModule));
        var second = ModuleKey.FromModuleType(typeof(AnotherDiagnosticModule));

        second.Should().NotBe(first);
        (second != first).Should().BeTrue();
    }

    [Fact]
    public void Equality_WhenTypesShareAFullNameAcrossAssemblies_ShouldRemainDistinct()
    {
        var firstType = CreateDynamicType("ModuleKeyCollisionAssemblyA");
        var secondType = CreateDynamicType("ModuleKeyCollisionAssemblyB");
        var first = ModuleKey.FromModuleType(firstType);
        var second = ModuleKey.FromModuleType(secondType);

        first.Value.Should().Be(second.Value);
        first.Should().NotBe(second);
        new Dictionary<ModuleKey, Type>
        {
            [first] = firstType,
            [second] = secondType
        }.Should().HaveCount(2);
    }

    [Fact]
    public void DefaultValue_ShouldExposeAnEmptyNonNullString()
    {
        var key = default(ModuleKey);

        key.Value.Should().BeEmpty();
        key.ToString().Should().BeEmpty();
    }

    private sealed class DiagnosticModule;

    private sealed class AnotherDiagnosticModule;

    private static Type CreateDynamicType(string assemblyName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule(assemblyName)
            .DefineType("Shared.Namespace.Module", TypeAttributes.Public)
            .CreateType()!;
    }
}
