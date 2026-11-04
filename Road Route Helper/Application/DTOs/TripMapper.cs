using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Application.DTOs;

public static class TripMapper
{
    public static TripDto ToDto(Trip trip) => new()
    {
        Id = trip.Id,
        Name = trip.Name,
        Description = trip.Description,
        StartDate = trip.StartDate,
        EndDate = trip.EndDate,
        StartingPointName = trip.StartingPointName,
        StartingLatitude = trip.StartingLatitude,
        StartingLongitude = trip.StartingLongitude,
        DestinationName = trip.DestinationName,
        DestinationLatitude = trip.DestinationLatitude,
        DestinationLongitude = trip.DestinationLongitude,
        Stops = trip.Stops.OrderBy(s => s.Order).Select(s => new StopDto
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            Notes = s.Notes,
            Order = s.Order,
        }).ToList(),
        RouteGeometry = trip.RouteGeometry.Select(p => new RoutePointDto { Latitude = p.Latitude, Longitude = p.Longitude }).ToList(),
        TotalDistanceKm = trip.TotalDistanceKm,
        EstimatedDuration = trip.EstimatedDuration,
        Notes = trip.Notes,
    };

    public static Trip FromDto(TripDto dto) => new()
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
        Stops = dto.Stops.OrderBy(s => s.Order).Select(s => new Stop
        {
            Id = s.Id == Guid.Empty ? Guid.NewGuid() : s.Id,
            Name = s.Name,
            Category = s.Category,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            Notes = s.Notes,
            Order = s.Order,
        }).ToList(),
        RouteGeometry = dto.RouteGeometry.Select(p => new RoutePoint { Latitude = p.Latitude, Longitude = p.Longitude }).ToList(),
        TotalDistanceKm = dto.TotalDistanceKm,
        EstimatedDuration = dto.EstimatedDuration,
        Notes = dto.Notes,
    };
}
