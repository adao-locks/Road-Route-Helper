using System;
using System.Collections.Generic;

namespace Road_Route_Helper.Domain
{
    public class Trip
    {
        public Guid Id { get; set; } = Guid.NewGuid();
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
        public List<Stop> Stops { get; set; } = new List<Stop>();
        public double TotalDistanceKm { get; set; }
        public TimeSpan? EstimatedDuration { get; set; }
        // Stored full route geometry (ordered list of lat/lng points following the road)
        public List<RoutePoint> RouteGeometry { get; set; } = new List<RoutePoint>();
        public string Notes { get; set; } = string.Empty;

        /// <summary>Independent copy with the same ids.</summary>
        public Trip DeepClone()
        {
            var clone = (Trip)MemberwiseClone();
            clone.Stops = Stops.ConvertAll(s => s.Clone());
            clone.RouteGeometry = RouteGeometry.ConvertAll(p => new RoutePoint { Latitude = p.Latitude, Longitude = p.Longitude });
            return clone;
        }

        /// <summary>Independent copy that is a new trip: new ids and a marked name.</summary>
        public Trip ToCopy()
        {
            var copy = DeepClone();
            copy.Id = Guid.NewGuid();
            copy.Name = Name + " (Copy)";
            copy.Stops.ForEach(s => s.Id = Guid.NewGuid());
            return copy;
        }
    }
}
