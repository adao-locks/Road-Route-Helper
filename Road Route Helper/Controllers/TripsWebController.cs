using Microsoft.AspNetCore.Mvc;
using Road_Route_Helper.Application.Interfaces;
using Road_Route_Helper.Application.Posters;
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
            SetRoutingServiceUrl();
            return View("~/Views/Trips/Create.cshtml");
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            SetRoutingServiceUrl();
            return View("~/Views/Trips/Edit.cshtml", trip);
        }

        [HttpGet("View/{id}")]
        public async Task<IActionResult> View(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            SetRoutingServiceUrl();
            return View("~/Views/Trips/View.cshtml", trip);
        }

        private void SetRoutingServiceUrl() =>
            ViewData["RoutingServiceUrl"] = _config.GetRequiredSection("Routing:ServiceUrl").Value;

        [HttpGet("View/{id}/poster.svg")]
        public async Task<IActionResult> PosterSvg(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            return Content(PosterSvgBuilder.Build(trip), "image/svg+xml");
        }
    }
}
