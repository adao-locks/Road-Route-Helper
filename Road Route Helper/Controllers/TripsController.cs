using Microsoft.AspNetCore.Mvc;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Application.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Road_Route_Helper.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TripsController : ControllerBase
    {
        private readonly ITripRepository _repo;

        public TripsController(ITripRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var trips = await _repo.GetAllAsync();
            return Ok(trips.Select(TripMapper.ToDto));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            return Ok(TripMapper.ToDto(trip));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TripDto dto)
        {
            var created = await _repo.CreateAsync(TripMapper.FromDto(dto));
            return CreatedAtAction(nameof(Get), new { id = created.Id }, TripMapper.ToDto(created));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TripDto dto)
        {
            var trip = TripMapper.FromDto(dto);
            trip.Id = id;
            var updated = await _repo.UpdateAsync(trip);
            if (updated == null) return NotFound();
            return Ok(TripMapper.ToDto(updated));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var ok = await _repo.DeleteAsync(id);
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpPost("{id}/duplicate")]
        public async Task<IActionResult> Duplicate(Guid id)
        {
            var copy = await _repo.DuplicateAsync(id);
            if (copy == null) return NotFound();
            return CreatedAtAction(nameof(Get), new { id = copy.Id }, TripMapper.ToDto(copy));
        }
    }
}
