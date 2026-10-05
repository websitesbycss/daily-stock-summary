using System.Net;
using System.Net.Sockets;

namespace DailyStockSummary.Api;

public static class ClientAddress
{
    private const string Unknown = "unknown";
    private const int Ipv6PrefixBytes = 8; // a /64: the part of an IPv6 address that identifies the network

    /// <summary>
    /// A stable key for rate limiting. Dual-stack servers report the same IPv4 client either as 10.0.0.1 or as
    /// ::ffff:10.0.0.1; both must map to one key or the client would get two allowances. An IPv6 client is keyed by
    /// its /64 network: one connection is typically assigned the whole /64, and keying on the full address would let
    /// it pick a fresh allowance by changing the low bits.
    /// </summary>
    public static string KeyFor(HttpContext httpContext)
    {
        var address = httpContext.Connection.RemoteIpAddress;

        if (address is null)
        {
            return Unknown;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, Ipv6PrefixBytes, bytes.Length - Ipv6PrefixBytes);
        return $"{new IPAddress(bytes)}/64";
    }
}
