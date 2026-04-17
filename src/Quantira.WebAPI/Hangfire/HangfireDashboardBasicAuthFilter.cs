using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;
using Microsoft.Extensions.Options;
using Quantira.WebAPI.Configuration;

namespace Quantira.WebAPI.Hangfire;

public sealed class HangfireDashboardBasicAuthFilter : IDashboardAuthorizationFilter
{
    private const string AuthenticationRealm = "Quantira Hangfire Dashboard";

    private readonly HangfireDashboardSettings _settings;
    private readonly IReadOnlyList<IpNetworkRule> _allowedNetworks;

    public HangfireDashboardBasicAuthFilter(IOptions<HangfireSettings> settings)
    {
        _settings = settings.Value.Dashboard;
        _allowedNetworks = _settings.AllowedIpNetworks
            .Where(network => !string.IsNullOrWhiteSpace(network))
            .Select(ParseNetwork)
            .ToList();
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (_settings.RequireSsl && !httpContext.Request.IsHttps)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }

        if (!IsRemoteIpAllowed(httpContext.Connection.RemoteIpAddress))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }

        if (!TryReadCredentials(httpContext.Request.Headers.Authorization, out var username, out var password))
        {
            Challenge(httpContext.Response);
            return false;
        }

        if (!ConstantTimeEquals(username, _settings.Username)
            || !ConstantTimeEquals(password, _settings.Password))
        {
            Challenge(httpContext.Response);
            return false;
        }

        return true;
    }

    private bool IsRemoteIpAllowed(IPAddress? remoteIpAddress)
    {
        if (_allowedNetworks.Count == 0)
            return true;

        if (remoteIpAddress is null)
            return false;

        var normalizedAddress = NormalizeRemoteIp(remoteIpAddress);

        return _allowedNetworks.Any(network => network.Contains(normalizedAddress));
    }

    private static void Challenge(HttpResponse response)
    {
        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.Headers.WWWAuthenticate = $"Basic realm=\"{AuthenticationRealm}\"";
    }

    private static bool TryReadCredentials(
        string? authorizationHeader,
        out string username,
        out string password)
    {
        username = string.Empty;
        password = string.Empty;

        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var encoded = authorizationHeader["Basic ".Length..].Trim();
            var decodedBytes = Convert.FromBase64String(encoded);
            var decoded = Encoding.UTF8.GetString(decodedBytes);
            var separatorIndex = decoded.IndexOf(':');

            if (separatorIndex <= 0)
                return false;

            username = decoded[..separatorIndex];
            password = decoded[(separatorIndex + 1)..];

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool ConstantTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static IPAddress NormalizeRemoteIp(IPAddress ipAddress)
        => ipAddress.IsIPv4MappedToIPv6
            ? ipAddress.MapToIPv4()
            : ipAddress;

    private static IpNetworkRule ParseNetwork(string value)
    {
        if (!IpNetworkRule.TryParse(value, out var rule)
            || rule is null)
            throw new InvalidOperationException(
                $"Invalid Hangfire dashboard allowed IP network '{value}'. Use CIDR notation such as '10.0.0.0/24' or '127.0.0.1/32'.");

        return rule;
    }

    private sealed class IpNetworkRule
    {
        private IpNetworkRule(IPAddress networkAddress, int prefixLength)
        {
            NetworkAddress = networkAddress;
            PrefixLength = prefixLength;
        }

        public IPAddress NetworkAddress { get; }

        public int PrefixLength { get; }

        public bool Contains(IPAddress ipAddress)
        {
            var normalizedAddress = NormalizeRemoteIp(ipAddress);

            if (normalizedAddress.AddressFamily != NetworkAddress.AddressFamily)
                return false;

            var candidateBytes = normalizedAddress.GetAddressBytes();
            var networkBytes = NetworkAddress.GetAddressBytes();
            var fullBytes = PrefixLength / 8;
            var remainingBits = PrefixLength % 8;

            for (var index = 0; index < fullBytes; index++)
            {
                if (candidateBytes[index] != networkBytes[index])
                    return false;
            }

            if (remainingBits == 0)
                return true;

            var mask = (byte)(0xFF << (8 - remainingBits));

            return (candidateBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
        }

        public static bool TryParse(string value, out IpNetworkRule? rule)
        {
            rule = null;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);

            if (!IPAddress.TryParse(parts[0], out var address))
                return false;

            var normalizedAddress = NormalizeRemoteIp(address);
            var maxPrefixLength = normalizedAddress.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
            var prefixLength = maxPrefixLength;

            if (parts.Length == 2
                && (!int.TryParse(parts[1], out prefixLength)
                    || prefixLength < 0
                    || prefixLength > maxPrefixLength))
                return false;

            rule = new IpNetworkRule(normalizedAddress, prefixLength);
            return true;
        }
    }
}