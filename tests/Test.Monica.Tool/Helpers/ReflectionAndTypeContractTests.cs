using Monica.Tool.Helpers;
using Monica.Tool.Reflection;

namespace Test.Monica.Tool.Helpers;

public sealed class ReflectionAndTypeContractTests
{
    [Fact]
    public void IsFunc_WhenDelegateHasArguments_ShouldRecognizeFuncFamily()
    {
        Func<int, string> func = value => value.ToString();

        TypeHelper.IsFunc(func).Should().BeTrue();
        TypeHelper.IsFunc<string>(func).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(double))]
    [InlineData(typeof(char))]
    [InlineData(typeof(Guid))]
    public void IsNonNullablePrimitiveType_WhenExtendedScalarIsProvided_ShouldReturnTrue(Type type)
    {
        TypeHelper.IsNonNullablePrimitiveType(type).Should().BeTrue();
    }

    [Fact]
    public void ImplementsGenericType_WhenTypeIsGenericContractItself_ShouldReturnTrue()
    {
        typeof(List<int>).ImplementsGenericType(typeof(List<>)).Should().BeTrue();
        typeof(List<int>).ImplementsGenericType(typeof(IEnumerable<>)).Should().BeTrue();
    }

    [Fact]
    public void GetValueByPath_WhenPathIsAbsolute_ShouldResolveNestedProperty()
    {
        var model = new OuterModel { Inner = new InnerModel { Value = "expected" } };
        var path = $"{typeof(OuterModel).FullName}.Inner.Value";

        ReflectionHelper.GetValueByPath(model, typeof(OuterModel), path).Should().Be("expected");
    }

    [Fact]
    public void PropertyInspection_WhenTypeHasIndexer_ShouldSkipIndexer()
    {
        var model = new IndexedModel { Value = 7 };

        var act = () => ObjectReflection.GetPublicPropertyValues(model).ToArray();

        act.Should().NotThrow();
        ObjectReflection.GetPublicPropertyValues(model).Should().Equal(7);
    }

    [Fact]
    public void TypeList_WhenTypeViolatesBaseConstraint_ShouldRejectIt()
    {
        var types = new TypeList<IDisposable>();

        var act = () => types.Add(typeof(string));

        act.Should().Throw<ArgumentException>();
    }

    private sealed class OuterModel
    {
        public required InnerModel Inner { get; init; }
    }

    private sealed class InnerModel
    {
        public required string Value { get; init; }
    }

    private sealed class IndexedModel
    {
        public int Value { get; init; }
        public int this[int index] => index;
    }
}
