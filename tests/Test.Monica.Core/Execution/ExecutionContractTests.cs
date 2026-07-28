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
        var descriptor = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            new ExecutionPoint("test.operation"),
            typeof(ExecutionContractTests),
            null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);

        Action create = () => _ = new ExecutionContext<int>(descriptor, 42);

        create.Should().Throw<ArgumentException>()
            .WithMessage("*does not match context input type*");
    }

    [Fact]
    public void ExecutionDescriptor_WhenMethodBoundaryIsEquivalent_ShouldReturnMemoizedInstance()
    {
        var point = new ExecutionPoint("test.memoized");

        var first = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            point,
            typeof(ExecutionContractTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
        for (var index = 0; index < 1_000; index++)
        {
            var repeated = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
                new ExecutionPoint("test.memoized"),
                typeof(ExecutionContractTests),
                entryMethod: null,
                isBusinessOperation: true,
                transactionMode: ExecutionTransactionMode.Automatic);

            repeated.Should().BeSameAs(first);
        }
    }

    [Fact]
    public void ExecutionDescriptor_WhenDefaultOperationNameIsUsed_ShouldReturnMemoizedInstance()
    {
        var first = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            new ExecutionPoint("test.default-name"),
            typeof(ExecutionContractTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);

        var second = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            new ExecutionPoint("test.default-name"),
            typeof(ExecutionContractTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);

        second.Should().BeSameAs(first);
        second.OperationName.Should().Be($"{typeof(ExecutionContractTests).FullName}.test.default-name");
    }

    [Fact]
    public void ExecutionDescriptor_ForInterface_ShouldMemoizeConcreteEntryMethod()
    {
        var first = ExecutionDescriptor.ForInterface<string, ExecutionUnit>(
            new ExecutionPoint("test.interface"),
            typeof(ExplicitComponent),
            typeof(IExplicitContract),
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
        for (var index = 0; index < 1_000; index++)
        {
            var repeated = ExecutionDescriptor.ForInterface<string, ExecutionUnit>(
                new ExecutionPoint("test.interface"),
                typeof(ExplicitComponent),
                typeof(IExplicitContract),
                isBusinessOperation: true,
                transactionMode: ExecutionTransactionMode.Automatic);

            repeated.Should().BeSameAs(first);
        }

        first.EntryMethod.Should().NotBeNull();
        first.EntryMethod!.DeclaringType.Should().Be(typeof(ExplicitComponent));
    }

    [Fact]
    public void ExecutionDescriptor_WhenBoundaryPolicyDiffers_ShouldReturnDistinctInstances()
    {
        var point = new ExecutionPoint("test.policy");
        var automatic = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            point,
            typeof(ExecutionContractTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
        var nonTransactional = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            point,
            typeof(ExecutionContractTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.None);
        var infrastructureOperation = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            point,
            typeof(ExecutionContractTests),
            entryMethod: null,
            isBusinessOperation: false,
            transactionMode: ExecutionTransactionMode.Automatic);

        nonTransactional.Should().NotBeSameAs(automatic);
        infrastructureOperation.Should().NotBeSameAs(automatic);
    }

    private interface IFeature;

    private interface IExplicitContract
    {
        Task ExecuteAsync(string input);
    }

    private sealed class ExplicitComponent : IExplicitContract
    {
        Task IExplicitContract.ExecuteAsync(string input)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record ConcreteFeature(string Value) : IFeature;
}
