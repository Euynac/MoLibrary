using Monica.Tool.Randomization;

namespace Test.Monica.Tool.Randomization;

public sealed class RandomizationContractTests
{
    [Fact]
    public void NextSecureBytes_ShouldHonorRequestedLengthAndValidateIt()
    {
        RandomValueGenerator.NextSecureBytes(0).Should().BeEmpty();
        RandomValueGenerator.NextSecureBytes(128).Should().HaveCount(128);

        var act = () => RandomValueGenerator.NextSecureBytes(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NextInt_WhenSeedHashIsMinimumInteger_ShouldStayInsideInclusiveRange()
    {
        var value = RandomValueGenerator.NextInt(10, 20, new MinimumHashCode());

        value.Should().BeInRange(10, 20);
    }

    [Fact]
    public void NextLong_WhenRangeSpansAllValues_ShouldReturnWithoutOverflow()
    {
        var act = () => RandomValueGenerator.NextLong(long.MinValue, long.MaxValue);

        act.Should().NotThrow();
    }

    [Fact]
    public void NextInt_WhenRangeSpansAllValues_ShouldReturnWithoutOverflow()
    {
        var act = () => RandomValueGenerator.NextInt(int.MinValue, int.MaxValue);

        act.Should().NotThrow();
    }

    [Fact]
    public void NextInt_WhenBoundsAreReversed_ShouldRejectThem()
    {
        var act = () => RandomValueGenerator.NextInt(2, 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NextDouble_WhenBoundsAreReversed_ShouldRejectThem()
    {
        var act = () => RandomValueGenerator.NextDouble(2, 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NextDouble_WhenFiniteRangeSpansBothExtremes_ShouldStayFiniteAndInRange()
    {
        var value = RandomValueGenerator.NextDouble(-double.MaxValue, double.MaxValue);

        value.Should().BeInRange(-double.MaxValue, double.MaxValue);
        double.IsFinite(value).Should().BeTrue();
    }

    [Fact]
    public void NextString_WhenCharacterGroupsAreSelected_ShouldUseOnlyThoseGroups()
    {
        var value = RandomValueGenerator.NextString(
            128,
            includeNumbers: false,
            includeLowercase: true,
            includeUppercase: false);

        value.Should().HaveLength(128).And.MatchRegex("^[a-z]+$");
    }

    [Fact]
    public void ShuffleRandom_WhenSeedIsRepeated_ShouldBeDeterministicAndNonDestructive()
    {
        ICollection<int> source = new[] { 1, 2, 3, 4, 5 };

        var first = source.ShuffleRandom("seed")!.ToArray();
        var second = source.ShuffleRandom("seed")!.ToArray();

        first.Should().Equal(second);
        first.Should().BeEquivalentTo(source);
        source.Should().Equal(1, 2, 3, 4, 5);
    }

    [Theory]
    [InlineData(-1d, false)]
    [InlineData(2d, true)]
    public void Chance_WhenProbabilityIsOutsideBounds_ShouldClamp(double probability, bool expected)
    {
        probability.Chance(hashSeed: "stable").Should().Be(expected);
    }

    private sealed class MinimumHashCode
    {
        public override int GetHashCode() => int.MinValue;
    }
}
