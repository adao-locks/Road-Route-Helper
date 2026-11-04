using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Road_Route_Helper.Application.Posters;
using Road_Route_Helper.Domain;
using Road_Route_Helper.Infrastructure;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests;

public class PosterEdgeCaseTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static Trip TripWith(params (double Lat, double Lng)[] stops) => new()
    {
        Name = "Viagem",
        Stops = stops.Select((s, i) => new Stop { Name = $"P{i}", Latitude = s.Lat, Longitude = s.Lng, Order = i }).ToList(),
    };

    private static (double X, double Y) Marker(string svg)
    {
        var circle = XDocument.Parse(svg).Descendants(Svg + "circle").First();
        return (double.Parse(circle.Attribute("cx")!.Value, CultureInfo.InvariantCulture),
                double.Parse(circle.Attribute("cy")!.Value, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Paradas_identicas_ficam_no_centro_do_poster() =>
        Assert.Equal((600.0, 400.0), Marker(PosterSvgBuilder.Build(TripWith((-27.5, -48.5), (-27.5, -48.5)))));

    [Fact]
    public void Uma_parada_fica_no_centro_do_poster() =>
        Assert.Equal((600.0, 400.0), Marker(PosterSvgBuilder.Build(TripWith((-27.5, -48.5)))));

    [Fact]
    public void Linha_horizontal_fica_centralizada_na_vertical()
    {
        var points = XDocument.Parse(PosterSvgBuilder.Build(TripWith((-27.5, -48.5), (-27.5, -49.5))))
            .Descendants(Svg + "polyline").First().Attribute("points")!.Value;
        Assert.All(points.Split(' '), p => Assert.EndsWith(",400.0", p));
    }

    [Fact]
    public void Viagem_sem_paradas_gera_poster_valido_com_titulo()
    {
        var document = XDocument.Parse(PosterSvgBuilder.Build(new Trip { Name = "Vazia" }));
        Assert.Equal(Svg + "svg", document.Root!.Name);
        Assert.Equal("1200", document.Root.Attribute("width")!.Value);
        Assert.Equal("Vazia", document.Descendants(Svg + "text").First().Value);
        Assert.Empty(document.Descendants(Svg + "polyline"));
        Assert.Empty(document.Descendants(Svg + "circle"));
    }
}

public class ApiValidationGapTests : IDisposable
{
    private readonly AppFactory _factory = new();
    private readonly HttpClient _client;

    public ApiValidationGapTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpResponseMessage> PostRaw(string json) =>
        await _client.PostAsync("/api/trips", new StringContent(json, Encoding.UTF8, "application/json"));

    [Theory]
    [InlineData("""{"name":"x","stops":[{"name":"a","category":99,"latitude":1,"longitude":1}]}""")]
    [InlineData("""{"name":"x","stops":[null]}""")]
    [InlineData("""{"name":"x","routeGeometry":[null]}""")]
    [InlineData("""{"name":"x","stops":[{"name":"","latitude":1,"longitude":1}]}""")]
    [InlineData("""{"name":"x","stops":[{"name":"   ","latitude":1,"longitude":1}]}""")]
    public async Task Dados_invalidos_retornam_400(string json) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRaw(json)).StatusCode);

    [Fact]
    public async Task Nome_de_parada_com_121_caracteres_retorna_400()
    {
        var json = $$"""{"name":"x","stops":[{"name":"{{new string('a', 121)}}","latitude":1,"longitude":1}]}""";
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRaw(json)).StatusCode);
    }

    [Fact]
    public async Task Put_com_parada_nula_retorna_400()
    {
        var response = await _client.PutAsync($"/api/trips/{Guid.NewGuid()}",
            new StringContent("""{"name":"x","stops":[null]}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"name":"x","stops":null}""", "Stops")]
    [InlineData("""{"name":"x","description":null}""", "Description")]
    [InlineData("""{"name":"x","notes":null}""", "Notes")]
    public async Task Campo_nulo_retorna_mensagem_em_portugues(string json, string field)
    {
        var response = await PostRaw(json);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, body);
        Assert.DoesNotContain("field is required", body);
    }
}

public class TripStoreContentValidationTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "rrh-" + Guid.NewGuid() + ".json");

    public void Dispose() => File.Delete(_file);

    private const string Id = "11111111-1111-1111-1111-111111111111";

    [Theory]
    [InlineData("""{"schemaVersion":1,"trips":[null]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":null}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","description":null}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","notes":null}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","stops":null}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","stops":[null]}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","stops":[{"name":null}]}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","stops":[{"name":"a","notes":null}]}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","stops":[{"name":"a","category":99}]}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","routeGeometry":null}]}""")]
    [InlineData($$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x","routeGeometry":[null]}]}""")]
    public void Conteudo_invalido_gera_erro_e_fica_intacto(string content)
    {
        File.WriteAllText(_file, content);
        var before = File.ReadAllBytes(_file);

        Assert.Throws<TripStoreException>(() => new JsonTripRepository(_file));

        Assert.Equal(before, File.ReadAllBytes(_file));
    }

    [Fact]
    public async Task Viagem_valida_com_campos_omitidos_carrega()
    {
        File.WriteAllText(_file, $$"""{"schemaVersion":1,"trips":[{"id":"{{Id}}","name":"x"}]}""");
        Assert.Single(await new JsonTripRepository(_file).GetAllAsync());
    }
}
