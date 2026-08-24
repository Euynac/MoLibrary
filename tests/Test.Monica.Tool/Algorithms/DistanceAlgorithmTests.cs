using Monica.Tool.Algorithms;

namespace Test.Monica.Tool.Algorithms;

public sealed class DistanceAlgorithmTests
{
    [Theory]
    [InlineData("", "", 0)]
    [InlineData("", "abc", 3)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("Saturday", "Sunday", 3)]
    public void Calculate_WhenStringsAreProvided_ShouldReturnLevenshteinDistance(
        string first,
        string second,
        int expected)
    {
        LevenshteinDistance.Calculate(first, second).Should().Be(expected);
        LevenshteinDistance.Calculate(second, first).Should().Be(expected);
    }

    [Fact]
    public void Similarity_WhenBothStringsAreEmpty_ShouldBeIdentical()
    {
        LevenshteinDistance.Similarity(string.Empty, string.Empty).Should().Be(1d);
    }

    [Fact]
    public void Similarity_WhenStringsPartiallyMatch_ShouldReturnFractionalScore()
    {
        LevenshteinDistance.Similarity("ab", "ac").Should().Be(0.5d);
    }

    [Fact]
    public void Calculate_WhenInputIsNull_ShouldRejectIt()
    {
        var act = () => LevenshteinDistance.Calculate(null!, "value");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EditSequence_WhenApplied_ShouldTransformSourceIntoTarget()
    {
        const string source = "kitten";
        const string target = "sitting";

        var operations = EditDistance.EditSequence(source, target);
        var transformed = new string(operations
            .Where(operation => operation.Operation is not EditDistance.EditOperationKind.Remove)
            .Select(operation => operation.ValueTo)
            .ToArray());

        transformed.Should().Be(target);
    }

    [Fact]
    public void EditSequence_WhenCostIsNegative_ShouldRejectIt()
    {
        var act = () => EditDistance.EditSequence("a", "b", insertCost: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EditSequence_WhenReplacementTiesWithRemoveAndAdd_ShouldPreferOneEdit()
    {
        EditDistance.EditSequence("a", "b")
            .Should().ContainSingle()
            .Which.Operation.Should().Be(EditDistance.EditOperationKind.Edit);
    }

    [Fact]
    public void EditSequence_WhenAddingOrRemovingNullCharacter_ShouldPreserveOperationKind()
    {
        EditDistance.EditSequence(string.Empty, "\0")
            .Should().ContainSingle()
            .Which.Operation.Should().Be(EditDistance.EditOperationKind.Add);
        EditDistance.EditSequence("\0", string.Empty)
            .Should().ContainSingle()
            .Which.Operation.Should().Be(EditDistance.EditOperationKind.Remove);
    }

    [Fact]
    public void CommonNormalize_WhenBoundsAreEqual_ShouldRejectUndefinedRange()
    {
        var normalizer = new Normalizer();

        var act = () => normalizer.CommonNormalize(1, 1, 1);

        act.Should().Throw<ArgumentException>();
    }
}
