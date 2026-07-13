using AwesomeAssertions;
using Monica.AI.UI.UIChat.Providers.Browser;

namespace Test.Monica.AI.UI.UIChat.Providers.Browser;

public sealed class BrowserChatHistoryCodecTests
{
    [Fact]
    public void Encode_WhenPayloadExceedsChunkSize_ShouldRoundTripAcrossChunks()
    {
        var value = new CodecValue(string.Join('|', Enumerable.Range(0, 2_000)));

        var encoded = BrowserChatHistoryCodec.Encode(value, chunkSize: 128);
        var decoded = BrowserChatHistoryCodec.Decode<CodecValue>(encoded.Chunks, encoded.Sha256);

        encoded.Chunks.Should().HaveCountGreaterThan(1);
        decoded.Should().Be(value);
    }

    [Fact]
    public void Decode_WhenPayloadWasModified_ShouldRejectChecksum()
    {
        var encoded = BrowserChatHistoryCodec.Encode(new CodecValue("history"));
        var chunks = encoded.Chunks.ToArray();
        chunks[0] = chunks[0][..^1] + (chunks[0][^1] == 'A' ? "B" : "A");

        var decode = () => BrowserChatHistoryCodec.Decode<CodecValue>(chunks, encoded.Sha256);

        decode.Should().Throw<Exception>();
    }

    private sealed record CodecValue(string Content);
}
