using AwesomeAssertions;
using Microsoft.Extensions.AI;
using Monica.AI.Providers;
using Monica.AI.Providers.OpenAI;
using OpenAI.Responses;

namespace Test.Monica.AI.Providers.OpenAI;

public sealed class OpenAIRequestOptionsChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_WhenUsingLocalHistory_ShouldDisableStoredOutput()
    {
        using var innerClient = new CapturingChatClient();
        using var client = new OpenAIRequestOptionsChatClient(
            innerClient,
            OpenAIProviderApiMode.Responses,
            OpenAIResponsesHistoryMode.LocalHistory,
            promptCacheKey: null,
            promptCacheRetention: null);

        _ = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "turn two")],
            cancellationToken: TestContext.Current.CancellationToken);

        var options = innerClient.CapturedOptions;
        options.Should().NotBeNull();
#pragma warning disable OPENAI001
        var rawFactory = options!.RawRepresentationFactory;
        rawFactory.Should().NotBeNull();
        var responseOptions = rawFactory!(innerClient)
            .Should().BeOfType<CreateResponseOptions>().Which;
        responseOptions.StoredOutputEnabled.Should().BeFalse();
        responseOptions.PreviousResponseId.Should().BeNull();
#pragma warning restore OPENAI001
    }

    [Fact]
    public async Task GetStreamingResponseAsync_WhenCompatibleEndpointHasUnknownTerminalReasoningStatus_ShouldKeepDeltas()
    {
        using var innerClient = new UnknownTerminalReasoningStatusChatClient();
        using var client = new OpenAIRequestOptionsChatClient(
            innerClient,
            OpenAIProviderApiMode.Responses,
            OpenAIResponsesHistoryMode.LocalHistory,
            promptCacheKey: null,
            promptCacheRetention: null);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
                           [new ChatMessage(ChatRole.User, "reason")],
                           cancellationToken: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        updates.SelectMany(static update => update.Contents)
            .OfType<TextReasoningContent>()
            .Select(static content => content.Text)
            .Should().ContainSingle().Which.Should().Be("thinking");
        updates.SelectMany(static update => update.Contents)
            .OfType<TextContent>()
            .Select(static content => content.Text)
            .Should().ContainSingle().Which.Should().Be("answer");
    }

    private sealed class CapturingChatClient : IChatClient
    {
        internal ChatOptions? CapturedOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CapturedOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CapturedOptions = options;
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(IChatClient) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class UnknownTerminalReasoningStatusChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent("thinking")]);
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("answer")]);
            await Task.Yield();
            throw new ArgumentOutOfRangeException(
                "value",
                string.Empty,
                "Unknown ReasoningStatus value.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(IChatClient) ? this : null;

        public void Dispose()
        {
        }
    }
}
