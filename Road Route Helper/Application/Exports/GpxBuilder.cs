using System.Globalization;
using System.Xml.Linq;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Application.Exports;

/// <summary>Writes a trip as GPX 1.1: stops become waypoints and the saved route becomes a track.</summary>
public static class GpxBuilder
{
    private static readonly XNamespace Gpx = "http://www.topografix.com/GPX/1/1";

    public static string Build(Trip trip)
    {
        var root = new XElement(Gpx + "gpx",
            new XAttribute("version", "1.1"),
            new XAttribute("creator", "Road Route Helper"),
            new XElement(Gpx + "metadata", new XElement(Gpx + "name", trip.Name)),
            trip.Stops.OrderBy(s => s.Order).Select(Waypoint));

        if (trip.RouteGeometry.Count > 0)
        {
            root.Add(new XElement(Gpx + "trk",
                new XElement(Gpx + "name", trip.Name),
                new XElement(Gpx + "trkseg", trip.RouteGeometry.Select(TrackPoint))));
        }

        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + root;
    }

    private static XElement Waypoint(Stop stop)
    {
        var waypoint = new XElement(Gpx + "wpt", Position(stop.Latitude, stop.Longitude), new XElement(Gpx + "name", stop.Name));
        if (!string.IsNullOrWhiteSpace(stop.Notes)) waypoint.Add(new XElement(Gpx + "desc", stop.Notes));
        return waypoint;
    }

    private static XElement TrackPoint(RoutePoint point) =>
        new(Gpx + "trkpt", Position(point.Latitude, point.Longitude));

    private static XAttribute[] Position(double latitude, double longitude) =>
    [
        new("lat", latitude.ToString("0.######", CultureInfo.InvariantCulture)),
        new("lon", longitude.ToString("0.######", CultureInfo.InvariantCulture)),
    ];
}
