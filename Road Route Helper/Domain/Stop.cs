using System;

namespace Road_Route_Helper.Domain
{
    public enum StopCategory
    {
        Restaurant,
        PhotoSpot,
        GasStation,
        Hotel,
        TouristAttraction,
        CoffeeShop,
        MotorcycleStop,
        Maintenance,
        Custom
    }

    public class Stop
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public StopCategory Category { get; set; } = StopCategory.PhotoSpot;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int Order { get; set; }

        public Stop Clone() => (Stop)MemberwiseClone();
    }
}
