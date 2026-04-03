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
            using var client = new TcpClient();
            var cancellationTokenSource = new CancellationTokenSource(timeout ?? new TimeSpan(0, 1, 0)).Token;
            await client.ConnectAsync(ip, port, cancellationTokenSource);
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}