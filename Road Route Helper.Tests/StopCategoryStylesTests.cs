using System.Reflection;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Application.Posters;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Tests;

public class StopCategoryStylesTests
{
    [Fact]
    public void Todas_as_categorias_tem_estilo()
    {
        foreach (var category in Enum.GetValues<StopCategory>())
        {
            Assert.Equal(category, StopCategoryStyles.For(category).Category);
        }
        Assert.Equal(9, StopCategoryStyles.All.Count);
    }

    [Fact]
    public void Poster_usa_o_estilo_da_categoria()
    {
        var trip = new Trip
        {
            Name = "Viagem",
            Stops = [new Stop { Name = "H", Latitude = -27.5, Longitude = -48.5, Category = StopCategory.Hotel }],
        };
        var svg = PosterSvgBuilder.Build(trip);
        Assert.Contains("fill='#06b6d4'", svg);
        Assert.Contains(">H</text>", svg);
    }

    [Fact]
    public void Categoria_tem_rotulo_em_portugues() =>
        Assert.Equal("Posto de combustível", StopCategoryStyles.For(StopCategory.GasStation).Label);

    [Theory]
    [InlineData(typeof(Trip))]
    [InlineData(typeof(TripDto))]
    public void Modelo_nao_expoe_inicio_nem_destino(Type type)
    {
        var names = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);
        Assert.DoesNotContain(names, n => n.StartsWith("Starting") || n.StartsWith("Destination"));
    }
}
