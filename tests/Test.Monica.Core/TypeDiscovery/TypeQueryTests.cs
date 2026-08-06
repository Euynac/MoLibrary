using AwesomeAssertions;
using Monica.Core.TypeDiscovery.Models;
using Xunit;

namespace Test.Monica.Core.TypeDiscovery;

public sealed class TypeQueryTests
{
    [Fact]
    public void AllOf_WhenOperandsAreNestedAndRepeated_ShouldProduceEquivalentNormalizedQuery()
    {
        var assignable = TypeQuery.All.AssignableTo<IDiscoveryMarker>();
        var attributed = TypeQuery.All.HasAttribute<InheritedDiscoveryAttribute>(inherit: true);
        var normalized = TypeQuery.AllOf(TypeQuery.ClosedClass, assignable, attributed);

        var nested = TypeQuery.AllOf(
            TypeQuery.All,
            TypeQuery.ClosedClass,
            TypeQuery.AllOf(assignable, attributed, assignable),
            TypeQuery.All);

        nested.Should().Be(normalized);
        nested.GetHashCode().Should().Be(normalized.GetHashCode());
        new HashSet<TypeQuery> { nested, normalized }.Should().ContainSingle();
        TypeQuery.Not(TypeQuery.Not(normalized)).Should().BeSameAs(normalized);
    }
}
