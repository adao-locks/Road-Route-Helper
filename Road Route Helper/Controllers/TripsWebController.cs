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

        public TripsWebController(ITripRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            // Views are stored under Views/Trips; specify full view path to avoid lookup by controller class name
            return View("~/Views/Trips/Create.cshtml");
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            return View("~/Views/Trips/Edit.cshtml", trip);
        }

        [HttpGet("View/{id}")]
        public async Task<IActionResult> View(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            return View("~/Views/Trips/View.cshtml", trip);
        }

        [HttpGet("View/{id}/poster.svg")]
        public async Task<IActionResult> PosterSvg(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            var stops = trip.Stops.OrderBy(s => s.Order).ToList();
            if (!stops.Any()) return Content("<svg></svg>", "image/svg+xml");

            // compute bounding box
            var lats = stops.Select(s => s.Latitude).ToList();
            var lngs = stops.Select(s => s.Longitude).ToList();
            var minLat = lats.Min(); var maxLat = lats.Max();
            var minLng = lngs.Min(); var maxLng = lngs.Max();
            var width = 1200; var height = 800;
            var pad = 60;
            double scaleX = (width - 2 * pad) / Math.Max(0.0001, maxLng - minLng);
            double scaleY = (height - 2 * pad) / Math.Max(0.0001, maxLat - minLat);
            double scale = Math.Min(scaleX, scaleY);

            string proj(double lat, double lng)
            {
                var x = (lng - minLng) * scale + pad;
                var y = height - ((lat - minLat) * scale + pad);
                return $"{x:F1},{y:F1}";
            }

            var path = string.Join(" ", stops.Select(s => proj(s.Latitude, s.Longitude)));

            var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>" +
                "<rect width='100%' height='100%' fill='#0f172a'/>" +
                $"<text x='{pad}' y='40' fill='#fff' font-size='36' font-family='Arial'>{System.Net.WebUtility.HtmlEncode(trip.Name)}</text>" +
                $"<polyline points='{path}' fill='none' stroke='#ffcc00' stroke-width='6' stroke-linecap='round' stroke-linejoin='round' opacity='0.95'/>";

            int idx = 1;
            foreach (var s in stops)
            {
                var p = proj(s.Latitude, s.Longitude);
                var color = "#ffffff";
                svg += $"<circle cx='{p.Split(',')[0]}' cy='{p.Split(',')[1]}' r='10' fill='{color}' stroke='#111827' stroke-width='3'/>";
                svg += $"<text x='{p.Split(',')[0]}' y='{double.Parse(p.Split(',')[1]) + 28:F1}' fill='#fff' font-size='18' font-family='Arial'>{idx}</text>";
                idx++;
            }

            svg += "</svg>";
            return Content(svg, "image/svg+xml");
        }
    }
}
