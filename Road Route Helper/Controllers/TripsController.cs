using Microsoft.AspNetCore.Mvc;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Application.Interfaces;
using Road_Route_Helper.Domain;
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
            var dtos = trips.Select(t => ToDto(t));
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var trip = await _repo.GetByIdAsync(id);
            if (trip == null) return NotFound();
            return Ok(ToDto(trip));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TripDto dto)
        {
            var trip = FromDto(dto);
            var created = await _repo.CreateAsync(trip);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, ToDto(created));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TripDto dto)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return NotFound();
            var trip = FromDto(dto);
            trip.Id = id;
            var updated = await _repo.UpdateAsync(trip);
            if (updated == null) return NotFound();
            return Ok(ToDto(updated));
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
            return CreatedAtAction(nameof(Get), new { id = copy.Id }, ToDto(copy));
        }

        private static TripDto ToDto(Trip t)
        {
            return new TripDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                StartingPointName = t.StartingPointName,
                StartingLatitude = t.StartingLatitude,
                StartingLongitude = t.StartingLongitude,
                DestinationName = t.DestinationName,
                DestinationLatitude = t.DestinationLatitude,
                DestinationLongitude = t.DestinationLongitude,
                Stops = t.Stops.Select(s => new Application.DTOs.StopDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Category = s.Category,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Notes = s.Notes,
                    Order = s.Order
                }).OrderBy(s => s.Order).ToList(),
                TotalDistanceKm = t.TotalDistanceKm,
                EstimatedDuration = t.EstimatedDuration,
                Notes = t.Notes
            };
        }

        private static Trip FromDto(TripDto dto)
        {
            var t = new Trip
            {
                Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                Name = dto.Name,
                Description = dto.Description,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                StartingPointName = dto.StartingPointName,
                StartingLatitude = dto.StartingLatitude,
                StartingLongitude = dto.StartingLongitude,
                DestinationName = dto.DestinationName,
                DestinationLatitude = dto.DestinationLatitude,
                DestinationLongitude = dto.DestinationLongitude,
                Stops = dto.Stops.Select(s => new Stop
                {
                    Id = s.Id == Guid.Empty ? Guid.NewGuid() : s.Id,
                    Name = s.Name,
                    Category = s.Category,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Notes = s.Notes,
                    Order = s.Order
                }).OrderBy(s => s.Order).ToList(),
                TotalDistanceKm = dto.TotalDistanceKm,
                EstimatedDuration = dto.EstimatedDuration,
                Notes = dto.Notes
            };
            return t;
        }
    }
}
