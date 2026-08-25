using System;
using System.Collections.Generic;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Application.DTOs
{
    public class StopDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public StopCategory Category { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public class TripDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string StartingPointName { get; set; } = string.Empty;
        public double? StartingLatitude { get; set; }
        public double? StartingLongitude { get; set; }
        public string DestinationName { get; set; } = string.Empty;
        public double? DestinationLatitude { get; set; }
        public double? DestinationLongitude { get; set; }
        public List<StopDto> Stops { get; set; } = new List<StopDto>();
        public double TotalDistanceKm { get; set; }
        public TimeSpan? EstimatedDuration { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
