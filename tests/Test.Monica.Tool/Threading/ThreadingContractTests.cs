using Monica.Tool.Threading;

namespace Test.Monica.Tool.Threading;

public sealed class ThreadingContractTests
{
    [Fact]
    public void ParallelSelect_WhenCountIsPositive_ShouldInvokeFunctionForEachItem()
    {
        var result = ParallelExecution.ParallelSelect(() => 42, 5).ToArray();

        result.Should().Equal(42, 42, 42, 42, 42);
    }

    [Fact]
    public void ParallelSelect_WhenCountIsNegative_ShouldRejectIt()
    {
        var act = () => ParallelExecution.ParallelSelect(() => 42, -1).ToArray();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParallelSelect_WhenFunctionIsNull_ShouldRejectItImmediately()
    {
        var act = () => ParallelExecution.ParallelSelect<int>(null!, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullDisposables_WhenDisposed_ShouldRemainReusableSingletons()
    {
        NullDisposable.Instance.Dispose();
        NullAsyncDisposable.Instance.DisposeAsync().IsCompletedSuccessfully.Should().BeTrue();

        NullDisposable.Instance.Should().BeSameAs(NullDisposable.Instance);
        NullAsyncDisposable.Instance.Should().BeSameAs(NullAsyncDisposable.Instance);
    }
}
