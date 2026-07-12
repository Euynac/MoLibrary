using AwesomeAssertions;
using Monica.AI.Models;

namespace Test.Monica.AI.Models;

public sealed class ChatTurnTests
{
    [Fact]
    public void Messages_WhenGenerationFailsAfterPartialOutput_ShouldKeepOutputBeforeError()
    {
        var turn = new ChatTurn(
            new AIChatMessage { Role = AIChatRole.User, Content = "question" },
            historyCheckpoint: 0)
        {
            AssistantMessage = new AIChatMessage
            {
                Role = AIChatRole.Assistant,
                Content = "partial answer"
            }
        };

        turn.AddError("provider failed").Should().BeTrue();
        turn.AddError("provider failed").Should().BeFalse();
        turn.AddError("retry failed").Should().BeTrue();

        turn.Messages.Select(static message => (message.Kind, message.Content))
            .Should().Equal(
                (AIChatMessageKind.Message, "question"),
                (AIChatMessageKind.Message, "partial answer"),
                (AIChatMessageKind.Error, "provider failed"),
                (AIChatMessageKind.Error, "retry failed"));
    }
}
