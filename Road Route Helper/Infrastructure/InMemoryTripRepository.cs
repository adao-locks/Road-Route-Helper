using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Road_Route_Helper.Application.Interfaces;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Infrastructure
{
    public class InMemoryTripRepository : ITripRepository
    {
        private readonly ConcurrentDictionary<Guid, Trip> _store = new();

        public InMemoryTripRepository()
        {
            // seed with a sample trip
            var sample = new Trip
            {
                Name = "Sample Coastal Ride",
                Description = "A short sample ride with a few stops.",
                StartDate = DateTimeOffset.Now,
                EndDate = DateTimeOffset.Now.AddDays(1),
                StartingPointName = "Start City",
                StartingLatitude = 34.0522,
                StartingLongitude = -118.2437,
                DestinationName = "End Town",
                DestinationLatitude = 34.4208,
                DestinationLongitude = -119.6982,
                Stops = new List<Stop>
                {
                    new Stop { Name = "Coffee Stop", Category = StopCategory.CoffeeShop, Latitude = 34.1, Longitude = -118.3, Order = 1 },
                    new Stop { Name = "Photo Overlook", Category = StopCategory.PhotoSpot, Latitude = 34.2, Longitude = -118.5, Order = 2 }
                },
                TotalDistanceKm = 120,
                EstimatedDuration = TimeSpan.FromHours(3),
                Notes = "Sample notes"
            };
            _store[sample.Id] = sample;
        }

        public Task<Trip> CreateAsync(Trip trip)
        {
            trip.Id = Guid.NewGuid();
            _store[trip.Id] = trip;
            return Task.FromResult(trip);
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            return Task.FromResult(_store.TryRemove(id, out _));
        }

        public Task<IEnumerable<Trip>> GetAllAsync()
        {
            return Task.FromResult(_store.Values.AsEnumerable());
        }

        public Task<Trip?> GetByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var trip);
            return Task.FromResult(trip);
        }

        public Task<Trip?> UpdateAsync(Trip trip)
        {
            if (!_store.ContainsKey(trip.Id)) return Task.FromResult<Trip?>(null);
            _store[trip.Id] = trip;
            return Task.FromResult<Trip?>(trip);
        }

        public Task<Trip?> DuplicateAsync(Guid id)
        {
            if (!_store.TryGetValue(id, out var original)) return Task.FromResult<Trip?>(null);
            var copy = new Trip
            {
                Id = Guid.NewGuid(),
                Name = original.Name + " (Copy)",
                Description = original.Description,
                StartDate = original.StartDate,
                EndDate = original.EndDate,
                StartingPointName = original.StartingPointName,
                StartingLatitude = original.StartingLatitude,
                StartingLongitude = original.StartingLongitude,
                DestinationName = original.DestinationName,
                DestinationLatitude = original.DestinationLatitude,
                DestinationLongitude = original.DestinationLongitude,
                Stops = original.Stops.Select(s => new Stop {
                    Id = Guid.NewGuid(),
                    Name = s.Name,
                    Category = s.Category,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Notes = s.Notes,
                    Order = s.Order
                }).ToList(),
                TotalDistanceKm = original.TotalDistanceKm,
                EstimatedDuration = original.EstimatedDuration,
                Notes = original.Notes
            };
            _store[copy.Id] = copy;
            return Task.FromResult<Trip?>(copy);
        }
    }
}
