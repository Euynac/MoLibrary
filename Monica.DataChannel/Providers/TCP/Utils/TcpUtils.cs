using System.Collections.Concurrent;
using System.Net;

namespace Monica.DataChannel.Providers.TCP.Utils;

/// <summary>
/// Provides stateless helpers for parsing TCP endpoint configuration.
/// </summary>
public static class TcpUtils
{
    /// <summary>
    /// Parses channel descriptors and network addresses into keyed connection metadata.
    /// </summary>
    /// <param name="channels">Channel descriptors in <c>name,role,isMain</c> form.</param>
    /// <param name="validAddresses">Network addresses in <c>host:port</c> form.</param>
    /// <returns>A connection metadata dictionary keyed by the derived channel name.</returns>
    /// <exception cref="ArgumentException">Thrown when the two collections have different lengths.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a descriptor or address is incomplete.</exception>
    public static ConcurrentDictionary<string, ConnectedExtend> ParseAddress(
        IReadOnlyList<string> channels,
        IReadOnlyList<string> validAddresses)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(validAddresses);

        if (channels.Count != validAddresses.Count)
        {
            throw new ArgumentException("Channel descriptors and network addresses must have the same number of entries.");
        }

        var addresses = new ConcurrentDictionary<string, ConnectedExtend>(StringComparer.Ordinal);
        for (var index = 0; index < channels.Count; index++)
        {
            var values = channels[index].Trim().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var addressValue = validAddresses[index].Split(':', StringSplitOptions.TrimEntries);
            var hostNameOrAddress = addressValue.ElementAtOrDefault(0);
            var portText = addressValue.ElementAtOrDefault(1);

            if (string.IsNullOrWhiteSpace(hostNameOrAddress) || string.IsNullOrWhiteSpace(portText))
            {
                throw new InvalidOperationException($"TCP address at index {index} must contain a host and port.");
            }

            var resolvedAddress = hostNameOrAddress == "0.0.0.0"
                ? IPAddress.Any
                : Dns.GetHostAddresses(hostNameOrAddress).FirstOrDefault()
                  ?? throw new InvalidOperationException($"TCP host '{hostNameOrAddress}' did not resolve to an address.");

            var connection = new ConnectedExtend
            {
                Address = Tuple.Create(resolvedAddress.ToString(), int.Parse(portText)),
                IsMainConnected = values.ElementAtOrDefault(2) == "1"
            };

            var name = values.ElementAtOrDefault(0);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"TCP channel descriptor at index {index} must contain a name.");
            }

            var suffix = $"{values.ElementAtOrDefault(1)}{values.ElementAtOrDefault(2)}";
            var key = string.IsNullOrWhiteSpace(suffix) ? name : $"{name}:{suffix}";
            addresses.TryAdd(key, connection);
        }

        return addresses;
    }
}

/// <summary>
/// Describes the resolved network address and primary-connection role of a TCP connection.
/// </summary>
public sealed class ConnectedExtend
{
    /// <summary>
    /// Gets or sets the resolved host and port.
    /// </summary>
    public Tuple<string, int>? Address { get; set; }

    /// <summary>
    /// Gets or sets whether the connection starts as the primary connection.
    /// </summary>
    public bool IsMainConnected { get; set; }
}
