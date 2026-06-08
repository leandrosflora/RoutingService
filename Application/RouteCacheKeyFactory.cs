using System.Security.Cryptography;
using System.Text;
using RoutingService.Contracts;

namespace RoutingService.Application;

public static class RouteCacheKeyFactory
{
    public static string Build(long networkVersion, SearchRoutesRequest request, DateTimeOffset requestedAt)
    {
        var normalizedPostalCode = new string(request.DestinationPostalCode.Where(char.IsDigit).ToArray());
        var requestedAtUtc = requestedAt.ToUniversalTime();
        var timeBucket = new DateTimeOffset(
            requestedAtUtc.Year,
            requestedAtUtc.Month,
            requestedAtUtc.Day,
            requestedAtUtc.Hour,
            requestedAtUtc.Minute,
            0,
            TimeSpan.Zero);

        var raw = string.Join(
            ":",
            networkVersion,
            request.OriginNodeId,
            normalizedPostalCode,
            request.Package.WeightKg,
            request.Package.CubicWeightKg,
            request.Package.IsFragile,
            request.Package.IsRestricted,
            timeBucket.ToUnixTimeSeconds());

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"route:{Convert.ToHexString(hash)}";
    }
}
