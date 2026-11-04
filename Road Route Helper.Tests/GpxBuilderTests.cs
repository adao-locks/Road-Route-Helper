using System.Net;
using System.Net.Http.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Application.Exports;
using Road_Route_Helper.Application.Formatting;
using Road_Route_Helper.Domain;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests;

public class GpxBuilderTests
{
    private static readonly XNamespace Gpx = "http://www.topografix.com/GPX/1/1";

    private static Trip Sample() => new()
    {
        Name = "São João & <Cia>",
        Stops =
        [
            new Stop { Name = "Segunda", Latitude = -27.6, Longitude = -48.6, Order = 1 },
            new Stop { Name = "Primeira", Latitude = -27.5, Longitude = -48.5, Order = 0, Notes = "nota" },
        ],
        RouteGeometry =
        [
            new RoutePoint { Latitude = -27.5, Longitude = -48.5 },
            new RoutePoint { Latitude = -27.55, Longitude = -48.55 },
            new RoutePoint { Latitude = -27.6, Longitude = -48.6 },
        ],
    };

    private static void AssertValidAgainstSchema(string gpx)
    {
        var schemas = new XmlSchemaSet();
        schemas.Add(Gpx.NamespaceName, Path.Combine(AppContext.BaseDirectory, "TestData", "gpx11.xsd"));
        var errors = new List<string>();
        XDocument.Parse(gpx).Validate(schemas, (_, e) => errors.Add(e.Message));
        Assert.Empty(errors);
    }

    [Fact]
    public void Gpx_e_valido_no_schema_1_1() => AssertValidAgainstSchema(GpxBuilder.Build(Sample()));

    [Fact]
    public void Em_ptBR_coordenadas_usam_ponto()
    {
        using var _ = Culture.Use("pt-BR");
        var first = XDocument.Parse(GpxBuilder.Build(Sample())).Descendants(Gpx + "wpt").First();
        Assert.Equal("-27.5", first.Attribute("lat")!.Value);
        Assert.Equal("-48.5", first.Attribute("lon")!.Value);
    }

    [Fact]
    public void Paradas_viram_wpt_na_ordem_da_viagem()
    {
        var names = XDocument.Parse(GpxBuilder.Build(Sample())).Descendants(Gpx + "wpt")
            .Select(w => w.Element(Gpx + "name")!.Value);
        Assert.Equal(["Primeira", "Segunda"], names);
    }

    [Fact]
    public void Tracado_vira_trk() =>
        Assert.Equal(3, XDocument.Parse(GpxBuilder.Build(Sample())).Descendants(Gpx + "trkpt").Count());

    [Fact]
    public void Sem_tracado_nao_gera_trk()
    {
        var trip = Sample();
        trip.RouteGeometry = [];
        var gpx = GpxBuilder.Build(trip);
        Assert.Empty(XDocument.Parse(gpx).Descendants(Gpx + "trk"));
        AssertValidAgainstSchema(gpx);
    }

    [Fact]
    public void Sem_paradas_gera_gpx_valido() => AssertValidAgainstSchema(GpxBuilder.Build(new Trip { Name = "Vazia" }));

    [Fact]
    public void Nome_especial_e_escapado()
    {
        var name = XDocument.Parse(GpxBuilder.Build(Sample())).Descendants(Gpx + "metadata").Single().Element(Gpx + "name")!.Value;
        Assert.Equal("São João & <Cia>", name);
    }

    [Theory]
    [InlineData("São João & <Cia>", "sao-joao-cia")]
    [InlineData("  Ração!!  ", "racao")]
    [InlineData("<<>>", "viagem")]
    [InlineData("", "viagem")]
    public void Slug_gera_nome_de_arquivo_seguro(string name, string expected) =>
        Assert.Equal(expected, FileNames.Slug(name));

    [Fact]
    public async Task Endpoint_retorna_anexo_e_404_para_id_inexistente()
    {
        using var factory = new AppFactory();
        var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/trips", TripSamples.Valid(name: "São João"))).Content.ReadFromJsonAsync<TripDto>();

        var response = await client.GetAsync($"/Trips/View/{created!.Id}/route.gpx");

        Assert.Equal("application/gpx+xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("sao-joao.gpx", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        AssertValidAgainstSchema(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Trips/View/{Guid.NewGuid()}/route.gpx")).StatusCode);
    }
}
