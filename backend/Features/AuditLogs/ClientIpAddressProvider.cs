using System.Net;

namespace IsoDocument.Api.Features.AuditLogs;

public interface IClientIpAddressProvider
{
    IPAddress? GetClientIpAddress();
}

public sealed class ClientIpAddressProvider(
    IHttpContextAccessor httpContextAccessor,
    bool allowDirectClientIp)
    : IClientIpAddressProvider
{
    public IPAddress? GetClientIpAddress()
    {
        var context = httpContextAccessor.HttpContext;
        if (allowDirectClientIp)
        {
            var directAddress = context?.Connection.RemoteIpAddress;
            if (directAddress is null)
            {
                return null;
            }

            var normalizedAddress = directAddress.IsIPv4MappedToIPv6
                ? directAddress.MapToIPv4()
                : directAddress;
            return IPAddress.IsLoopback(normalizedAddress) ? null : normalizedAddress;
        }

        // Nginx sets this header only after its realip module accepts a trusted
        // upstream proxy. Missing verification must not become a proxy's IP.
        var value = context?.Request.Headers["X-Verified-Client-IP"].ToString();
        if (!IPAddress.TryParse(value, out var address))
        {
            return null;
        }

        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
