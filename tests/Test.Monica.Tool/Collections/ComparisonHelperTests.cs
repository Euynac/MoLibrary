using Monica.Tool.Collections;

namespace Test.Monica.Tool.Collections;

public sealed class ComparisonHelperTests
{
    [Fact]
    public void CompareToObjAsc_WhenBothValuesAreNull_ShouldAllowComparisonChainToContinue()
    {
        object chain = new();

        var continuation = chain.CompareToObjAsc(null, null, out var result);

        result.Should().Be(0);
        continuation.Should().NotBeNull();
    }

    [Fact]
    public void CompareToObjDesc_WhenComparerReturnsMinimumInteger_ShouldInvertOnlyItsSign()
    {
        var comparison = new MinimumComparable().CompareToObj(new object(), isDesc: true);

        comparison.Should().Be(1);
    }

    [Theory]
    [InlineData(null, "value", true, 1)]
    [InlineData("value", null, true, -1)]
    [InlineData(null, "value", false, -1)]
    [InlineData("value", null, false, 1)]
    public void CompareToObj_WhenOneValueIsNull_ShouldHonorNullPlacement(
        string? first,
        string? second,
        bool nullIsLast,
        int expected)
    {
        first.CompareToObj(second, nullIsLast: nullIsLast).Should().Be(expected);
    }

    [Fact]
    public void ChainedComparisons_ShouldContinueOnlyForEqualValues()
    {
        object chain = new();

        var equalContinuation = chain.CompareToObjDesc("same", "same", out var equalResult);
        var unequalContinuation = chain.CompareToObjDesc("b", "a", out var unequalResult);

        equalResult.Should().Be(0);
        equalContinuation.Should().NotBeNull();
        unequalResult.Should().Be(-1);
        unequalContinuation.Should().BeNull();
    }

    [Fact]
    public void CompareToObj_ShouldBeAntisymmetric()
    {
        var forward = "a".CompareToObj("b");
        var reverse = "b".CompareToObj("a");

        forward.Should().Be(-reverse);
    }

    private sealed class MinimumComparable : IComparable
    {
        public int CompareTo(object? obj) => int.MinValue;
    }
}
