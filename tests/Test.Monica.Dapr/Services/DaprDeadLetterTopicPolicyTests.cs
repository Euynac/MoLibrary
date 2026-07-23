using Monica.Dapr.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Dapr.Services;

public sealed class DaprDeadLetterTopicPolicyTests
{
    [Fact]
    public void Resolve_WhenPolicyIsDisabled_ShouldReturnNull()
    {
        var policy = DaprDeadLetterTopicPolicy.Create(new ModuleDaprEventBusOption());

        var result = policy.Resolve("orders");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_WhenSuffixIsConfigured_ShouldAppendSuffix()
    {
        var policy = DaprDeadLetterTopicPolicy.Create(new ModuleDaprEventBusOption
        {
            DeadLetterTopicSuffix = ".dead-letter"
        });

        var result = policy.Resolve("orders");

        Assert.Equal("orders.dead-letter", result);
    }

    [Fact]
    public void Resolve_WhenSourceIsTerminalTopic_ShouldReturnNull()
    {
        var policy = DaprDeadLetterTopicPolicy.Create(new ModuleDaprEventBusOption
        {
            DeadLetterTopicSuffix = ".dead-letter"
        });

        var result = policy.Resolve("orders.dead-letter");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".dead letter")]
    public void Create_WhenSuffixIsEmptyOrContainsWhitespace_ShouldThrow(string suffix)
    {
        var options = new ModuleDaprEventBusOption
        {
            DeadLetterTopicSuffix = suffix
        };

        Assert.Throws<ArgumentException>(() => DaprDeadLetterTopicPolicy.Create(options));
    }
}
