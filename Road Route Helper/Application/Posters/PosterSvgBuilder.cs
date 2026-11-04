using System.Globalization;
using System.Security;
using System.Text;
using Road_Route_Helper.Application.Formatting;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Application.Posters;

/// <summary>Renders a trip as an SVG poster. Output never depends on the current culture.</summary>
public static class PosterSvgBuilder
{
    private const int Width = 1200;
    private const int Height = 800;
    private const int Pad = 40;
    private const double EarthRadius = 6378137.0;
    private const double MaxLatitude = 85.05;
    private const double SimplifyToleranceMeters = 8.0;
    private const string FontFamily = "Helvetica, Arial, sans-serif";
    private static readonly CultureInfo DisplayCulture = new("pt-BR");

    public static string Build(Trip trip)
    {
        var stops = trip.Stops.OrderBy(s => s.Order).ToList();
        var geometry = trip.RouteGeometry.Count >= 2 ? trip.RouteGeometry : [];
        if (stops.Count == 0 && geometry.Count == 0) return "<svg></svg>";

        var stopPoints = stops.Select(s => new RoutePoint { Latitude = s.Latitude, Longitude = s.Longitude }).ToList();
        var line = geometry.Count > 0 ? Simplify(geometry) : stopPoints;
        var view = Viewport.Fit(line.Concat(stopPoints).ToList(), 0, 0, Width, Height, Pad);

        var svg = new StringBuilder();
        Append(svg, $"<svg xmlns='http://www.w3.org/2000/svg' width='{Width}' height='{Height}' viewBox='0 0 {Width} {Height}'>");
        svg.Append(Definitions);
        svg.Append("<rect width='100%' height='100%' fill='url(#bgGrad)'/>");
        Append(svg, $"<text x='{Pad}' y='48' fill='#ffffff' font-size='40' font-family='{FontFamily}' font-weight='700'>{SecurityElement.Escape(trip.Name)}</text>");
        Append(svg, $"<text x='{Pad}' y='80' fill='#cbd5e1' font-size='16' font-family='{FontFamily}'>{SecurityElement.Escape(Subtitle(trip))}</text>");
        Append(svg, $"<polyline points='{view.Path(line)}' fill='none' stroke='url(#routeGrad)' stroke-width='8' stroke-linecap='round' stroke-linejoin='round' filter='url(#glow)'/>");
        AppendMarkers(svg, stops, view);
        AppendLegend(svg, trip);
        AppendInset(svg, line);
        svg.Append("</svg>");
        return svg.ToString();
    }

    private const string Definitions =
        "<defs>" +
        "<linearGradient id='bgGrad' x1='0' x2='0' y1='0' y2='1'><stop offset='0%' stop-color='#071129'/><stop offset='100%' stop-color='#081926'/></linearGradient>" +
        "<linearGradient id='routeGrad' x1='0' x2='1' y1='0' y2='0'><stop offset='0%' stop-color='#ffd166'/><stop offset='50%' stop-color='#ffb703'/><stop offset='100%' stop-color='#ff7b00'/></linearGradient>" +
        "<filter id='glow' x='-50%' y='-50%' width='200%' height='200%'><feGaussianBlur stdDeviation='4' result='coloredBlur'/><feMerge><feMergeNode in='coloredBlur'/><feMergeNode in='SourceGraphic'/></feMerge></filter>" +
        "</defs>";

    private static void Append(StringBuilder svg, FormattableString text) => svg.Append(text.ToString(CultureInfo.InvariantCulture));

    private static string Subtitle(Trip trip)
    {
        if (!string.IsNullOrWhiteSpace(trip.Description)) return trip.Description;
        var dates = new[] { trip.StartDate, trip.EndDate }
            .Where(d => d != null)
            .Select(d => d!.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        return string.Join(" – ", dates);
    }

    private static void AppendMarkers(StringBuilder svg, List<Stop> stops, Viewport view)
    {
        for (var i = 0; i < stops.Count; i++)
        {
            var (x, y) = view.Project(stops[i].Latitude, stops[i].Longitude);
            var (letter, color) = CategoryStyle(stops[i].Category);
            Append(svg, $"<circle cx='{x:F1}' cy='{y:F1}' r='12' fill='{color}' stroke='#0b1220' stroke-width='2'/>");
            Append(svg, $"<text x='{x:F1}' y='{y + 5:F1}' fill='#fff' font-size='12' font-family='{FontFamily}' text-anchor='middle'>{SecurityElement.Escape(letter)}</text>");
            Append(svg, $"<text x='{x:F1}' y='{y + 28:F1}' fill='#e2e8f0' font-size='14' font-family='{FontFamily}' text-anchor='middle'>{i + 1}</text>");
        }
    }

    private static (string Letter, string Color) CategoryStyle(StopCategory category) => category switch
    {
        StopCategory.Restaurant => ("R", "#ef4444"),
        StopCategory.GasStation => ("G", "#f97316"),
        StopCategory.PhotoSpot => ("P", "#f59e0b"),
        StopCategory.Hotel => ("H", "#06b6d4"),
        StopCategory.TouristAttraction => ("T", "#8b5cf6"),
        StopCategory.CoffeeShop => ("C", "#a16207"),
        StopCategory.MotorcycleStop => ("M", "#10b981"),
        StopCategory.Maintenance => ("!", "#ef4444"),
        _ => ("•", "#6b7280"),
    };

    private static void AppendLegend(StringBuilder svg, Trip trip)
    {
        var parts = new List<string>();
        if (trip.TotalDistanceKm > 0) parts.Add(trip.TotalDistanceKm.ToString("F1", DisplayCulture) + " km");
        if (trip.EstimatedDuration is { } duration) parts.Add(DurationFormatter.Format(duration));
        if (parts.Count == 0) return;

        var x = Pad;
        var y = Height - Pad - 14;
        Append(svg, $"<rect x='{x - 12}' y='{y - 28}' rx='8' ry='8' width='260' height='48' fill='rgba(0,0,0,0.18)' stroke='rgba(255,255,255,0.04)'/>");
        Append(svg, $"<text x='{x}' y='{y - 8}' fill='#e2e8f0' font-size='14' font-family='{FontFamily}'>{SecurityElement.Escape(string.Join(" • ", parts))}</text>");
    }

    private static void AppendInset(StringBuilder svg, List<RoutePoint> line)
    {
        if (line.Count == 0) return;
        const int width = 340, height = 220;
        var left = Width - Pad - width;
        var top = Height - Pad - height;
        var view = Viewport.Fit(line, left, top, width, height, 12);
        Append(svg, $"<rect x='{left}' y='{top}' width='{width}' height='{height}' rx='8' ry='8' fill='rgba(255,255,255,0.03)' stroke='rgba(255,255,255,0.06)'/>");
        Append(svg, $"<polyline points='{view.Path(line)}' fill='none' stroke='url(#routeGrad)' stroke-width='3' stroke-linecap='round' stroke-linejoin='round'/>");
    }

    private static double MercatorX(double longitude) => EarthRadius * longitude * Math.PI / 180.0;

    private static double MercatorY(double latitude)
    {
        var radians = Math.Clamp(latitude, -MaxLatitude, MaxLatitude) * Math.PI / 180.0;
        return EarthRadius * Math.Log(Math.Tan(Math.PI / 4.0 + radians / 2.0));
    }

    /// <summary>Ramer-Douglas-Peucker in Mercator meters: shrinks the SVG while keeping the road's shape.</summary>
    private static List<RoutePoint> Simplify(List<RoutePoint> points)
    {
        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var pending = new Stack<(int First, int Last)>();
        pending.Push((0, points.Count - 1));
        while (pending.Count > 0)
        {
            var (first, last) = pending.Pop();
            var (farthest, distance) = FarthestPoint(points, first, last);
            if (distance <= SimplifyToleranceMeters) continue;
            keep[farthest] = true;
            pending.Push((first, farthest));
            pending.Push((farthest, last));
        }
        return points.Where((_, i) => keep[i]).ToList();
    }

    private static (int Index, double Distance) FarthestPoint(List<RoutePoint> points, int first, int last)
    {
        double x1 = MercatorX(points[first].Longitude), y1 = MercatorY(points[first].Latitude);
        double x2 = MercatorX(points[last].Longitude), y2 = MercatorY(points[last].Latitude);
        var length = Math.Sqrt((y2 - y1) * (y2 - y1) + (x2 - x1) * (x2 - x1));
        var best = (Index: first, Distance: -1.0);
        for (var i = first + 1; i < last; i++)
        {
            double x0 = MercatorX(points[i].Longitude), y0 = MercatorY(points[i].Latitude);
            var distance = length == 0
                ? Math.Sqrt((x0 - x1) * (x0 - x1) + (y0 - y1) * (y0 - y1))
                : Math.Abs((y2 - y1) * x0 - (x2 - x1) * y0 + x2 * y1 - y2 * x1) / length;
            if (distance > best.Distance) best = (i, distance);
        }
        return best;
    }

    /// <summary>Maps coordinates into a rectangle of the canvas, centered and with a uniform scale.</summary>
    private sealed record Viewport(double MinX, double MinY, double Scale, double Left, double Bottom, double OffsetX, double OffsetY)
    {
        public static Viewport Fit(List<RoutePoint> points, double left, double top, double width, double height, double pad)
        {
            var xs = points.Select(p => MercatorX(p.Longitude)).ToList();
            var ys = points.Select(p => MercatorY(p.Latitude)).ToList();
            double innerWidth = width - 2 * pad, innerHeight = height - 2 * pad;
            var spanX = Math.Max(1e-6, xs.Max() - xs.Min());
            var spanY = Math.Max(1e-6, ys.Max() - ys.Min());
            var scale = Math.Min(innerWidth / spanX, innerHeight / spanY);
            return new Viewport(
                xs.Min(), ys.Min(), scale, left, top + height,
                pad + (innerWidth - spanX * scale) / 2.0,
                pad + (innerHeight - spanY * scale) / 2.0);
        }

        public (double X, double Y) Project(double latitude, double longitude) => (
            Left + OffsetX + (MercatorX(longitude) - MinX) * Scale,
            Bottom - OffsetY - (MercatorY(latitude) - MinY) * Scale);

        public string Path(IEnumerable<RoutePoint> points) => string.Join(" ", points.Select(p =>
        {
            var (x, y) = Project(p.Latitude, p.Longitude);
            return string.Create(CultureInfo.InvariantCulture, $"{x:F1},{y:F1}");
        }));
    }
}
