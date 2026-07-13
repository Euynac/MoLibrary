using AwesomeAssertions;
using Monica.AI.Chat.Models;

namespace Test.Monica.AI.Chat.Models;

public sealed class ChatHistoryWriteResultTests
{
    [Fact]
    public void PersistenceDisabled_WhenPersistenceIsNotConfigured_ShouldExposeStructuredReason()
    {
        var result = ChatHistoryWriteResult.PersistenceDisabled;

        result.Status.Should().Be(ChatHistoryWriteStatus.NotPersisted);
        result.FailureReason.Should().Be(ChatHistoryWriteFailureReason.PersistenceDisabled);
        result.IsPersisted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenCallerMutatesPrunedSessionIds_ShouldRetainOwnedValues()
    {
        var prunedSessionIds = new List<string> { "session-1" };
        var result = new ChatHistoryWriteResult
        {
            Status = ChatHistoryWriteStatus.Succeeded,
            PrunedSessionIds = prunedSessionIds
        };

        prunedSessionIds.Clear();

        result.PrunedSessionIds.Should().Equal("session-1");
    }
}
