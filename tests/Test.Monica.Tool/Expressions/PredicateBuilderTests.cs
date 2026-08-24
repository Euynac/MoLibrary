using System.Linq.Expressions;
using Monica.Tool.Expressions;

namespace Test.Monica.Tool.Expressions;

public sealed class PredicateBuilderTests
{
    [Fact]
    public void And_WhenExpressionsUseDifferentParameters_ShouldRebindAndCompose()
    {
        Expression<Func<int, bool>> positive = value => value > 0;
        Expression<Func<int, bool>> even = number => number % 2 == 0;

        var predicate = positive.And(even).Compile();

        predicate(2).Should().BeTrue();
        predicate(1).Should().BeFalse();
        predicate(-2).Should().BeFalse();
    }

    [Fact]
    public void Extend_WhenOperatorIsInvalid_ShouldRejectIt()
    {
        Expression<Func<int, bool>> first = value => value > 0;
        Expression<Func<int, bool>> second = value => value < 10;

        var act = () => first.Extend(second, (PredicateOperator)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExpressionStarter_WhenStartedTwice_ShouldRejectSecondStart()
    {
        var starter = PredicateBuilder.New<int>();
        starter.Start(value => value > 0);

        var act = () => starter.Start(value => value < 10);

        act.Should().Throw<InvalidOperationException>();
    }
}
