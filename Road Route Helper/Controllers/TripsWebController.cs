using Microsoft.AspNetCore.Mvc;
using Road_Route_Helper.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Road_Route_Helper.Controllers
{
    [Route("Trips")]
    public class TripsWebController : Controller
    {
        private readonly ITripRepository _repo;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

        public TripsWebController(ITripRepository repo, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _repo = repo;
            _config = config;
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            // Provide routing service url to the view (configurable via appsettings or environment)
            var service = _config["Routing:ServiceUrl"] ?? "https://router.project-osrm.org/route/v1";
            ViewData["RoutingServiceUrl"] = service;
            return View("~/Views/Trips/Create.cshtml");
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            var service = _config["Routing:ServiceUrl"] ?? "https://router.project-osrm.org/route/v1";
            ViewData["RoutingServiceUrl"] = service;
            return View("~/Views/Trips/Edit.cshtml", trip);
        }

        [HttpGet("View/{id}")]
        public async Task<IActionResult> View(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            var service = _config["Routing:ServiceUrl"] ?? "https://router.project-osrm.org/route/v1";
            ViewData["RoutingServiceUrl"] = service;
            return View("~/Views/Trips/View.cshtml", trip);
        }

        [HttpGet("View/{id}/poster.svg")]
        public async Task<IActionResult> PosterSvg(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            var stops = trip.Stops.OrderBy(s => s.Order).ToList();
            var geomPoints = trip.RouteGeometry ?? new System.Collections.Generic.List<Road_Route_Helper.Domain.RoutePoint>();

            if ((!stops.Any()) && (!geomPoints.Any())) return Content("<svg></svg>", "image/svg+xml");

            // choose source of geometry: prefer route geometry (followed road), otherwise use stops
            var useGeom = geomPoints.Any();
            // compute bounding box using Web Mercator projection for better fidelity on large areas
            // convert lat/lng to Mercator meters
            const double R = 6378137.0;
            static double MercatorX(double lon) => R * lon * Math.PI / 180.0;
            static double MercatorY(double lat)
            {
                var latRad = lat * Math.PI / 180.0;
                return R * Math.Log(Math.Tan(Math.PI / 4.0 + latRad / 2.0));
            }

            var mercXs = (useGeom ? geomPoints.Select(p => MercatorX(p.Longitude)) : stops.Select(s => MercatorX(s.Longitude))).ToList();
            var mercYs = (useGeom ? geomPoints.Select(p => MercatorY(p.Latitude)) : stops.Select(s => MercatorY(s.Latitude))).ToList();
            var minX = mercXs.Min(); var maxX = mercXs.Max();
            var minY = mercYs.Min(); var maxY = mercYs.Max();
            var width = 1200; var height = 800;
            // dynamic padding based on canvas size
            var pad = Math.Max(40, (int)(Math.Min(width, height) * 0.05));

            // ensure bbox has non-zero size
            var spanX = Math.Max(1e-6, maxX - minX);
            var spanY = Math.Max(1e-6, maxY - minY);

            double scaleX = (width - 2 * pad) / spanX;
            double scaleY = (height - 2 * pad) / spanY;
            double scale = Math.Min(scaleX, scaleY);

            // compute offsets to center the geometry within available area
            var usedWidth = spanX * scale;
            var usedHeight = spanY * scale;
            var offsetX = ((width - 2 * pad) - usedWidth) / 2.0;
            var offsetY = ((height - 2 * pad) - usedHeight) / 2.0;

            string proj(double lat, double lng)
            {
                var mx = MercatorX(lng);
                var my = MercatorY(lat);
                var x = (mx - minX) * scale + pad + offsetX;
                // invert Y for SVG coordinate system
                var y = height - ((my - minY) * scale + pad + offsetY);
                return $"{x:F1},{y:F1}";
            }

            // simplify geometry to reduce SVG size while preserving shape (Ramer-Douglas-Peucker)
            List<Road_Route_Helper.Domain.RoutePoint> simplGeom = null;
            if (useGeom)
            {
                double toleranceMeters = 8.0; // 8 meters default tolerance

                // prepare mercator arrays
                var mcoords = geomPoints.Select(p => new { p, x = MercatorX(p.Longitude), y = MercatorY(p.Latitude) }).ToList();

                List<Road_Route_Helper.Domain.RoutePoint> RdpReduce(int first, int last)
                {
                    if (first >= last) return new List<Road_Route_Helper.Domain.RoutePoint>();
                    double x1 = mcoords[first].x, y1 = mcoords[first].y;
                    double x2 = mcoords[last].x, y2 = mcoords[last].y;
                    double maxDist = -1; int index = -1;
                    for (int i = first + 1; i < last; i++)
                    {
                        double x0 = mcoords[i].x, y0 = mcoords[i].y;
                        double num = Math.Abs((y2 - y1) * x0 - (x2 - x1) * y0 + x2 * y1 - y2 * x1);
                        double den = Math.Sqrt((y2 - y1) * (y2 - y1) + (x2 - x1) * (x2 - x1));
                        double dist = den == 0 ? Math.Sqrt((x0 - x1) * (x0 - x1) + (y0 - y1) * (y0 - y1)) : num / den;
                        if (dist > maxDist)
                        {
                            index = i; maxDist = dist;
                        }
                    }
                    if (maxDist > toleranceMeters)
                    {
                        var left = RdpReduce(first, index);
                        var right = RdpReduce(index, last);
                        var result = new List<Road_Route_Helper.Domain.RoutePoint>();
                        if (left.Any()) result.AddRange(left.Take(left.Count - 1));
                        if (right.Any()) result.AddRange(right);
                        return result;
                    }
                    else
                    {
                        return new List<Road_Route_Helper.Domain.RoutePoint> { mcoords[first].p, mcoords[last].p };
                    }
                }

                try
                {
                    simplGeom = RdpReduce(0, mcoords.Count - 1);
                }
                catch
                {
                    simplGeom = geomPoints;
                }
            }

            var pathPoints = useGeom
                ? (simplGeom ?? geomPoints).Select(p => proj(p.Latitude, p.Longitude)).ToList()
                : stops.Select(s => proj(s.Latitude, s.Longitude)).ToList();
            var path = string.Join(" ", pathPoints);

            // build SVG with gradients and subtle shadow for a premium poster look
            var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>" +
                "<defs>" +
                  // background gradient
                  "<linearGradient id='bgGrad' x1='0' x2='0' y1='0' y2='1'>" +
                    "<stop offset='0%' stop-color='#071129'/>" +
                    "<stop offset='100%' stop-color='#081926'/>" +
                  "</linearGradient>" +
                  // route gradient
                  "<linearGradient id='routeGrad' x1='0' x2='1' y1='0' y2='0'>" +
                    "<stop offset='0%' stop-color='#ffd166'/>" +
                    "<stop offset='50%' stop-color='#ffb703'/>" +
                    "<stop offset='100%' stop-color='#ff7b00'/>" +
                  "</linearGradient>" +
                  // soft glow filter for route
                  "<filter id='glow' x='-50%' y='-50%' width='200%' height='200%'>" +
                    "<feGaussianBlur stdDeviation='4' result='coloredBlur'/>" +
                    "<feMerge>" +
                      "<feMergeNode in='coloredBlur'/>" +
                      "<feMergeNode in='SourceGraphic'/>" +
                    "</feMerge>" +
                  "</filter>" +
                "</defs>" +
                // background
                $"<rect width='100%' height='100%' fill='url(#bgGrad)'/>" +
                // title
                $"<text x='{pad}' y='48' fill='#ffffff' font-size='40' font-family='Helvetica, Arial, sans-serif' font-weight='700'>{System.Net.WebUtility.HtmlEncode(trip.Name)}</text>" +
                // subtitle (description or dates)
                $"<text x='{pad}' y='80' fill='#cbd5e1' font-size='16' font-family='Helvetica, Arial, sans-serif'>{System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(trip.Description) ? (trip.StartDate?.ToString("yyyy-MM-dd") + (trip.EndDate != null ? " – " + trip.EndDate?.ToString("yyyy-MM-dd") : "") ) : trip.Description)}</text>" +
                // route polyline with glow and gradient stroke
                $"<polyline points='{path}' fill='none' stroke='url(#routeGrad)' stroke-width='8' stroke-linecap='round' stroke-linejoin='round' opacity='1' filter='url(#glow)'/>";

            // draw stop markers (use stops list) so markers are at meaningful points
            int idx = 1;
            foreach (var s in stops)
            {
                var p = proj(s.Latitude, s.Longitude);
                var parts = p.Split(',');
                var cx = parts[0];
                var cy = parts[1];
                svg += $"<circle cx='{cx}' cy='{cy}' r='10' fill='#ffffff' stroke='#111827' stroke-width='3'/>";
                svg += $"<text x='{cx}' y='{double.Parse(cy) + 28:F1}' fill='#fff' font-size='18' font-family='Arial'>{idx}</text>";
                idx++;
            }

            // legend: distance and duration
            try
            {
                var distText = trip.TotalDistanceKm > 0 ? $"{trip.TotalDistanceKm:F1} km" : string.Empty;
                var durText = trip.EstimatedDuration != null ? ((TimeSpan)trip.EstimatedDuration).ToString(@"hh\:mm\:ss") : string.Empty;
                var legend = string.Join(" • ", new[] { distText, durText }.Where(x => !string.IsNullOrEmpty(x)));
                if (!string.IsNullOrEmpty(legend))
                {
                    var lx = pad;
                    var ly = height - pad - 14;
                    svg += $"<rect x='{lx - 12}' y='{ly - 28}' rx='8' ry='8' width='260' height='48' fill='rgba(0,0,0,0.18)' stroke='rgba(255,255,255,0.04)'/>";
                    svg += $"<text x='{lx}' y='{ly - 8}' fill='#e2e8f0' font-size='14' font-family='Helvetica, Arial, sans-serif'>{System.Net.WebUtility.HtmlEncode(legend)}</text>";
                }
            }
            catch { }

            // inset mini-map (bottom-right)
            try
            {
                var insetW = 340; var insetH = 220; var insetPad = 12;
                var insetX = width - pad - insetW; var insetY = height - pad - insetH;
                var geomForInset = useGeom ? (simplGeom ?? geomPoints) : stops.Select(s => new Road_Route_Helper.Domain.RoutePoint { Latitude = s.Latitude, Longitude = s.Longitude }).ToList();
                if (geomForInset != null && geomForInset.Any())
                {
                    var ixXs = geomForInset.Select(p => MercatorX(p.Longitude)).ToList();
                    var ixYs = geomForInset.Select(p => MercatorY(p.Latitude)).ToList();
                    var iMinX = ixXs.Min(); var iMaxX = ixXs.Max();
                    var iMinY = ixYs.Min(); var iMaxY = ixYs.Max();
                    var iSpanX = Math.Max(1e-6, iMaxX - iMinX);
                    var iSpanY = Math.Max(1e-6, iMaxY - iMinY);
                    var iScale = Math.Min((insetW - 2 * insetPad) / iSpanX, (insetH - 2 * insetPad) / iSpanY);
                    var iOffsetX = ((insetW - 2 * insetPad) - iSpanX * iScale) / 2.0;
                    var iOffsetY = ((insetH - 2 * insetPad) - iSpanY * iScale) / 2.0;
                    string iproj(double lat, double lng)
                    {
                        var mx = MercatorX(lng); var my = MercatorY(lat);
                        var x = (mx - iMinX) * iScale + insetPad + iOffsetX + insetX;
                        var y = insetY + insetH - ((my - iMinY) * iScale + insetPad + iOffsetY);
                        return $"{x:F1},{y:F1}";
                    }
                    var insetPath = string.Join(" ", geomForInset.Select(p => iproj(p.Latitude, p.Longitude)));
                    svg += $"<g transform='translate(0,0)'>" +
                        $"<rect x='{insetX}' y='{insetY}' width='{insetW}' height='{insetH}' rx='8' ry='8' fill='rgba(255,255,255,0.03)' stroke='rgba(255,255,255,0.06)'/>" +
                        $"<polyline points='{insetPath}' fill='none' stroke='url(#routeGrad)' stroke-width='3' stroke-linecap='round' stroke-linejoin='round'/>" +
                        "</g>";
                }
            }
            catch { }

            svg += "</svg>";
            return Content(svg, "image/svg+xml");
        }
    }
}
