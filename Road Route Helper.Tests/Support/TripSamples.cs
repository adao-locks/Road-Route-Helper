using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Tests.Support;

public static class TripSamples
{
    public static TripDto Valid(string name = "Viagem", int stops = 2) => new()
    {
        Name = name,
        Stops = Enumerable.Range(0, stops).Select(i => new StopDto
        {
            Name = $"Parada {i + 1}",
            Category = StopCategory.PhotoSpot,
            Latitude = -27.5 - i * 0.1,
            Longitude = -48.5 - i * 0.1,
            Order = i,
        }).ToList(),
    };
}
