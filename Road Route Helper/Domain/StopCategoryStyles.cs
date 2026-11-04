namespace Road_Route_Helper.Domain;

public record StopCategoryStyle(StopCategory Category, string Label, string Letter, string Color);

/// <summary>The single definition of how each category looks, used by the poster and the maps.</summary>
public static class StopCategoryStyles
{
    public static IReadOnlyList<StopCategoryStyle> All { get; } =
    [
        new(StopCategory.Restaurant, "Restaurante", "R", "#ef4444"),
        new(StopCategory.PhotoSpot, "Ponto de foto", "P", "#f59e0b"),
        new(StopCategory.GasStation, "Posto de combustível", "G", "#f97316"),
        new(StopCategory.Hotel, "Hotel", "H", "#06b6d4"),
        new(StopCategory.TouristAttraction, "Atração turística", "T", "#8b5cf6"),
        new(StopCategory.CoffeeShop, "Café", "C", "#a16207"),
        new(StopCategory.MotorcycleStop, "Parada de moto", "M", "#10b981"),
        new(StopCategory.Maintenance, "Manutenção", "!", "#ef4444"),
        new(StopCategory.Custom, "Outro", "•", "#6b7280"),
    ];

    public static StopCategoryStyle For(StopCategory category) => All.First(s => s.Category == category);
}
