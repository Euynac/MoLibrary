using System.Reflection;
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
    public void ExecutionDescriptor_WhenDefaultDisplayNameIsUsed_ShouldReturnMemoizedInstance()
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
        second.DisplayName.Should().Be($"{typeof(ExecutionContractTests).FullName}.test.default-name");
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

    [Fact]
    public void ExecutionDescriptor_WhenOverloadOrExecutionPointDiffers_ShouldUseDistinctOperationKeys()
    {
        var stringOverload = GetOverload(typeof(string));
        var integerOverload = GetOverload(typeof(int));

        var first = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            new ExecutionPoint("test.identity.first"),
            typeof(ExecutionContractTests),
            stringOverload,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
        var overloaded = ExecutionDescriptor.ForMethod<int, ExecutionUnit>(
            new ExecutionPoint("test.identity.first"),
            typeof(ExecutionContractTests),
            integerOverload,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
        var otherPoint = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            new ExecutionPoint("test.identity.second"),
            typeof(ExecutionContractTests),
            stringOverload,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);

        first.DisplayName.Should().Be(overloaded.DisplayName);
        first.OperationKey.Should().NotBe(overloaded.OperationKey);
        first.OperationKey.Should().NotBe(otherPoint.OperationKey);
    }

    [Fact]
    public void ExecutionContext_WhenCreatedForSameDescriptor_ShouldUseDistinctInvocationIds()
    {
        var descriptor = ExecutionDescriptor.ForMethod<string, ExecutionUnit>(
            new ExecutionPoint("test.invocation"),
            typeof(ExecutionContractTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);

        var first = new ExecutionContext<string>(descriptor, "first");
        var second = new ExecutionContext<string>(descriptor, "second");

        first.InvocationId.Should().NotBe(Guid.Empty);
        second.InvocationId.Should().NotBe(Guid.Empty);
        first.InvocationId.Should().NotBe(second.InvocationId);
    }

    [Fact]
    public void ExecutionDescriptor_WhenContractDiffers_ShouldUseDistinctOperationKeys()
    {
        var first = ExecutionDescriptor.ForInterface<string, ExecutionUnit>(
            new ExecutionPoint("test.contract-identity"),
            typeof(DualContractComponent),
            typeof(IFirstContract),
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
        var second = ExecutionDescriptor.ForInterface<string, ExecutionUnit>(
            new ExecutionPoint("test.contract-identity"),
            typeof(DualContractComponent),
            typeof(ISecondContract),
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);

        first.EntryMethod.Should().BeSameAs(second.EntryMethod);
        first.ContractType.Should().Be(typeof(IFirstContract));
        second.ContractType.Should().Be(typeof(ISecondContract));
        first.OperationKey.Should().NotBe(second.OperationKey);
    }

    private static MethodInfo GetOverload(Type parameterType)
    {
        return typeof(ExecutionContractTests)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method =>
                method.Name == nameof(OverloadedOperation)
                && method.GetParameters() is [{ ParameterType: var type }]
                && type == parameterType);
    }

    private static void OverloadedOperation(string value)
    {
    }

    private static void OverloadedOperation(int value)
    {
    }

    private interface IFeature;

    private interface IExplicitContract
    {
        Task ExecuteAsync(string input);
    }

    private interface IFirstContract
    {
        Task ExecuteAsync(string input);
    }

    private interface ISecondContract
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

    private sealed class DualContractComponent : IFirstContract, ISecondContract
    {
        public Task ExecuteAsync(string input)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record ConcreteFeature(string Value) : IFeature;
}
