using AwesomeAssertions;
using Monica.Core.Execution;
using Xunit;

namespace Test.Monica.Core.Execution;

public sealed class ExecutionContractTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" mediator.request")]
    [InlineData("mediator.request ")]
    public void ExecutionPoint_WhenValueIsNotStable_ShouldRejectIt(string value)
    {
        Action create = () => _ = new ExecutionPoint(value);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExecutionFeatureCollection_ShouldUseTheExactFeatureContract()
    {
        var features = new ExecutionFeatureCollection();
        var feature = new ConcreteFeature("topic");

        features.Set(feature);

        features.GetRequired<ConcreteFeature>().Should().BeSameAs(feature);
        features.TryGet<IFeature>(out _).Should().BeFalse();
        features.Count.Should().Be(1);
        features.Remove<ConcreteFeature>().Should().BeTrue();
        features.Count.Should().Be(0);
    }

    [Fact]
    public void ExecutionContext_WhenDescriptorInputDoesNotMatch_ShouldRejectIt()
    {
        var descriptor = new ExecutionDescriptor(
            new ExecutionPoint("test.operation"),
            "test.operation",
            typeof(ExecutionContractTests),
            null,
            typeof(string),
            typeof(ExecutionUnit),
            isBusinessOperation: true,
            isLongRunning: false);

        Action create = () => _ = new ExecutionContext<int>(descriptor, 42);

        create.Should().Throw<ArgumentException>()
            .WithMessage("*does not match context input type*");
    }

    private interface IFeature;

    private sealed record ConcreteFeature(string Value) : IFeature;
}
