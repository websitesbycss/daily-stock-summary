namespace DailyStockSummary.Api;

public static class ClientAddress
{
    private const string Unknown = "unknown";

    /// <summary>
    /// A stable key for rate limiting. Dual-stack servers report the same IPv4 client either as 10.0.0.1 or as
    /// ::ffff:10.0.0.1; both must map to one key or the client would get two allowances.
    /// </summary>
    public static string KeyFor(HttpContext httpContext)
    {
        var address = httpContext.Connection.RemoteIpAddress;

        if (address is null)
        {
            return Unknown;
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}
