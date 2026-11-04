using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace Road_Route_Helper.Tests.Support;

/// <summary>Replaces the OSRM routing service with a canned response, so E2E tests stay offline from it.</summary>
public static class OsrmStub
{
    public sealed class Calls
    {
        public int Count;
    }

    public static async Task<Calls> ReturnRouteAsync(IPage page, double distanceMeters, double durationSeconds, int delayMs = 0)
    {
        var calls = new Calls();
        await page.RouteAsync("**/route/v1/**", async route =>
        {
            Interlocked.Increment(ref calls.Count);
            if (delayMs > 0) await Task.Delay(delayMs);
            await route.FulfillAsync(new RouteFulfillOptions
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                Body = BuildResponse(route.Request.Url, distanceMeters, durationSeconds),
            });
        });
        return calls;
    }

    /// <summary>Keeps the first request unanswered until <paramref name="release"/> completes.</summary>
    public static async Task<Calls> HoldFirstRouteAsync(IPage page, Task release, double heldDistanceMeters, double nextDistanceMeters)
    {
        var calls = new Calls();
        await page.RouteAsync("**/route/v1/**", async route =>
        {
            var isFirst = Interlocked.Increment(ref calls.Count) == 1;
            if (isFirst) await release;
            await route.FulfillAsync(new RouteFulfillOptions
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                Body = BuildResponse(route.Request.Url, isFirst ? heldDistanceMeters : nextDistanceMeters, 60),
            });
        });
        return calls;
    }

    public static Task FailAsync(IPage page) =>
        page.RouteAsync("**/route/v1/**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 500,
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
            Body = "{}",
        }));

    private static string BuildResponse(string url, double distance, double duration)
    {
        var path = new Uri(url).AbsolutePath;
        var coords = path[(path.LastIndexOf('/') + 1)..].Split(';')
            .Select(c => c.Split(','))
            .Select(p => (Lng: double.Parse(p[0], CultureInfo.InvariantCulture), Lat: double.Parse(p[1], CultureInfo.InvariantCulture)))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            code = "Ok",
            routes = new[]
            {
                new
                {
                    distance,
                    duration,
                    geometry = Encode(coords),
                    legs = Enumerable.Range(0, Math.Max(1, coords.Count - 1))
                        .Select(_ => new { steps = Array.Empty<object>(), distance, duration, summary = "" }),
                },
            },
            waypoints = coords.Select(c => new { location = new[] { c.Lng, c.Lat }, name = "" }),
        });
    }

    /// <summary>Google polyline encoding with precision 5, the format OSRM responds with.</summary>
    private static string Encode(IEnumerable<(double Lng, double Lat)> coords)
    {
        var sb = new StringBuilder();
        int lastLat = 0, lastLng = 0;
        foreach (var (lng, lat) in coords)
        {
            int latE5 = (int)Math.Round(lat * 1e5), lngE5 = (int)Math.Round(lng * 1e5);
            EncodeValue(sb, latE5 - lastLat);
            EncodeValue(sb, lngE5 - lastLng);
            lastLat = latE5;
            lastLng = lngE5;
        }
        return sb.ToString();
    }

    private static void EncodeValue(StringBuilder sb, int value)
    {
        var v = value < 0 ? ~(value << 1) : value << 1;
        while (v >= 0x20)
        {
            sb.Append((char)((0x20 | (v & 0x1f)) + 63));
            v >>= 5;
        }
        sb.Append((char)(v + 63));
    }
}
