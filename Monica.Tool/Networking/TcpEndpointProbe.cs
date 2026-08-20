using System.Net.Sockets;

namespace Monica.Tool.Networking;

public class TcpEndpointProbe
{
    /// <summary>
    /// Returns null to indicate success, otherwise returns an error message
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public static async Task<string?> TestIpAndPort(string ip, int port, TimeSpan? timeout = null)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ip);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);

            using var client = new TcpClient();
            using var cancellationTokenSource = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(1));
            await client.ConnectAsync(ip, port, cancellationTokenSource.Token).ConfigureAwait(false);
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
