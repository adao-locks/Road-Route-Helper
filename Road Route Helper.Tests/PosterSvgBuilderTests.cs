using System.Globalization;
using System.Xml.Linq;
using Road_Route_Helper.Application.Posters;
using Road_Route_Helper.Domain;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests;

public class PosterSvgBuilderTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static Trip TripWith(params (double Lat, double Lng)[] stops) => new()
    {
        Name = "Viagem",
        Stops = stops.Select((s, i) => new Stop { Name = $"P{i}", Latitude = s.Lat, Longitude = s.Lng, Order = i }).ToList(),
    };

    private static List<(double X, double Y)> LinePoints(string svg)
    {
        var line = XDocument.Parse(svg).Descendants(Svg + "polyline").First();
        return line.Attribute("points")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair =>
            {
                var parts = pair.Split(',');
                Assert.Equal(2, parts.Length);
                return (double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture));
            }).ToList();
    }

    private static List<(double X, double Y)> Markers(string svg) =>
        XDocument.Parse(svg).Descendants(Svg + "circle")
            .Select(c => (
                double.Parse(c.Attribute("cx")!.Value, CultureInfo.InvariantCulture),
                double.Parse(c.Attribute("cy")!.Value, CultureInfo.InvariantCulture)))
            .ToList();

    [Fact]
    public void Em_ptBR_cada_ponto_da_linha_tem_x_e_y()
    {
        using var _ = Culture.Use("pt-BR");
        var points = LinePoints(PosterSvgBuilder.Build(TripWith((-27.5, -48.5), (-27.61, -48.73), (-27.8, -48.9))));
        Assert.Equal(3, points.Count);
    }

    [Fact]
    public void Em_ptBR_marcadores_ficam_dentro_do_canvas()
    {
        using var _ = Culture.Use("pt-BR");
        var markers = Markers(PosterSvgBuilder.Build(TripWith((-27.5, -48.5), (-27.61, -48.73))));
        Assert.Equal(2, markers.Count);
        Assert.All(markers, m =>
        {
            Assert.InRange(m.X, 0, 1200);
            Assert.InRange(m.Y, 0, 800);
        });
    }

    [Fact]
    public void Marcador_coincide_com_o_ponto_da_rota()
    {
        using var _ = Culture.Use("pt-BR");
        var trip = TripWith((-27.5, -48.5));
        trip.RouteGeometry =
        [
            new RoutePoint { Latitude = -27.5, Longitude = -48.5 },
            new RoutePoint { Latitude = -27.9, Longitude = -48.1 },
        ];
        var svg = PosterSvgBuilder.Build(trip);
        var first = LinePoints(svg)[0];
        var marker = Markers(svg)[0];
        Assert.InRange(Math.Abs(first.X - marker.X), 0, 0.1);
        Assert.InRange(Math.Abs(first.Y - marker.Y), 0, 0.1);
    }

    [Fact]
    public void Viagem_sem_paradas_nem_tracado_gera_svg_vazio() =>
        Assert.Equal("<svg></svg>", PosterSvgBuilder.Build(new Trip { Name = "Vazia" }));

    [Fact]
    public void Uma_parada_gera_svg_valido()
    {
        var svg = PosterSvgBuilder.Build(TripWith((-27.5, -48.5)));
        Assert.Single(Markers(svg));
        Assert.DoesNotContain("NaN", svg);
    }

    [Fact]
    public void Tracado_de_um_ponto_e_tratado_como_ausente()
    {
        var trip = TripWith((-27.5, -48.5), (-27.6, -48.6));
        trip.RouteGeometry = [new RoutePoint { Latitude = 0, Longitude = 0 }];
        Assert.Equal(2, LinePoints(PosterSvgBuilder.Build(trip)).Count);
    }

    [Fact]
    public void Paradas_identicas_geram_numeros_finitos()
    {
        var svg = PosterSvgBuilder.Build(TripWith((-27.5, -48.5), (-27.5, -48.5)));
        Assert.DoesNotContain("NaN", svg);
        Assert.DoesNotContain("Infinity", svg);
        Assert.All(Markers(svg), m =>
        {
            Assert.InRange(m.X, 0, 1200);
            Assert.InRange(m.Y, 0, 800);
        });
    }

    [Fact]
    public void Latitude_90_nao_gera_infinito()
    {
        var svg = PosterSvgBuilder.Build(TripWith((90, 0), (-90, 10)));
        Assert.DoesNotContain("NaN", svg);
        Assert.DoesNotContain("Infinity", svg);
        Assert.DoesNotContain("∞", svg);
    }

    [Fact]
    public void Nome_com_caracteres_especiais_e_escapado()
    {
        var trip = TripWith((-27.5, -48.5));
        trip.Name = "São João & <Cia>";
        var title = XDocument.Parse(PosterSvgBuilder.Build(trip)).Descendants(Svg + "text").First().Value;
        Assert.Equal("São João & <Cia>", title);
    }

    [Fact]
    public void Duracao_de_25h_aparece_como_25h()
    {
        var trip = TripWith((-27.5, -48.5));
        trip.EstimatedDuration = TimeSpan.FromHours(25);
        Assert.Contains("25h 00min", PosterSvgBuilder.Build(trip));
    }
}
