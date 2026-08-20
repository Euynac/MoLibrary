using Monica.Tool.Networking;

namespace Test.Monica.Tool.Networking;

public sealed class TcpEndpointProbeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task TestIpAndPort_WhenPortIsInvalid_ShouldReturnValidationError(int port)
    {
        var error = await TcpEndpointProbe.TestIpAndPort("127.0.0.1", port, TimeSpan.Zero);

        error.Should().NotBeNullOrWhiteSpace();
    }
}
