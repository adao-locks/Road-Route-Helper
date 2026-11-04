using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Domain;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests;

public class TripsApiTests : IDisposable
{
    private readonly AppFactory _factory = new();
    private readonly HttpClient _client;

    public TripsApiTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> Post(TripDto dto) => _client.PostAsJsonAsync("/api/trips", dto);

    private static async Task<JsonElement> Errors(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors");
    }

    [Fact]
    public async Task Post_com_nome_vazio_retorna_400_com_erro_em_Name()
    {
        var response = await Post(TripSamples.Valid(name: ""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Errors(response)).TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Post_com_nome_de_121_caracteres_retorna_400()
    {
        var response = await Post(TripSamples.Valid(name: new string('a', 121)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_com_latitude_999_na_parada_retorna_400()
    {
        var dto = TripSamples.Valid();
        dto.Stops[0].Latitude = 999;
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(dto)).StatusCode);
    }

    [Fact]
    public async Task Post_com_ponto_de_rota_invalido_retorna_400()
    {
        var dto = TripSamples.Valid();
        dto.RouteGeometry = [new RoutePointDto { Latitude = 0, Longitude = 181 }];
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(dto)).StatusCode);
    }

    [Fact]
    public async Task Post_com_data_final_anterior_retorna_400_com_a_mensagem()
    {
        var dto = TripSamples.Valid();
        dto.StartDate = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);
        dto.EndDate = dto.StartDate.Value.AddDays(-1);
        var response = await Post(dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("A data final deve ser igual ou posterior à inicial.", (await Errors(response)).ToString());
    }

    [Fact]
    public async Task Post_com_data_final_igual_a_inicial_retorna_201()
    {
        var dto = TripSamples.Valid();
        dto.StartDate = dto.EndDate = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.Created, (await Post(dto)).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Post_com_poucas_paradas_retorna_201(int stops) =>
        Assert.Equal(HttpStatusCode.Created, (await Post(TripSamples.Valid(stops: stops))).StatusCode);

    [Fact]
    public async Task Parada_sem_categoria_vira_Custom()
    {
        const string json = """{"name":"x","stops":[{"name":"a","latitude":1,"longitude":1,"order":0}]}""";
        var response = await _client.PostAsync("/api/trips", new StringContent(json, Encoding.UTF8, "application/json"));
        var created = await response.Content.ReadFromJsonAsync<TripDto>();
        Assert.Equal(StopCategory.Custom, created!.Stops[0].Category);
    }

    [Fact]
    public async Task Duracao_de_um_dia_e_uma_hora_e_aceita()
    {
        const string json = """{"name":"x","estimatedDuration":"1.01:00:00","stops":[]}""";
        var response = await _client.PostAsync("/api/trips", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TripDto>();
        Assert.Equal(TimeSpan.FromHours(25), created!.EstimatedDuration);
    }

    [Fact]
    public async Task Duplicar_mantem_o_tracado()
    {
        var dto = TripSamples.Valid();
        dto.RouteGeometry =
        [
            new RoutePointDto { Latitude = -27.5, Longitude = -48.5 },
            new RoutePointDto { Latitude = -27.6, Longitude = -48.6 },
        ];
        var created = await (await Post(dto)).Content.ReadFromJsonAsync<TripDto>();
        var copyResponse = await _client.PostAsync($"/api/trips/{created!.Id}/duplicate", null);
        var copy = await copyResponse.Content.ReadFromJsonAsync<TripDto>();

        Assert.NotEqual(created.Id, copy!.Id);
        Assert.Equal(2, copy.RouteGeometry.Count);
        Assert.Equal(-27.6, copy.RouteGeometry[1].Latitude);
    }

    [Fact]
    public async Task Put_mantem_todos_os_campos_enviados()
    {
        var dto = TripSamples.Valid();
        dto.Description = "desc";
        dto.Notes = "notas";
        dto.TotalDistanceKm = 123.45;
        dto.EstimatedDuration = TimeSpan.FromHours(3);
        dto.Stops[0].Category = StopCategory.Hotel;
        dto.Stops[0].Notes = "nota da parada";
        var created = await (await Post(TripSamples.Valid())).Content.ReadFromJsonAsync<TripDto>();

        var updated = await (await _client.PutAsJsonAsync($"/api/trips/{created!.Id}", dto)).Content.ReadFromJsonAsync<TripDto>();

        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal("desc", updated.Description);
        Assert.Equal("notas", updated.Notes);
        Assert.Equal(123.45, updated.TotalDistanceKm);
        Assert.Equal(StopCategory.Hotel, updated.Stops[0].Category);
        Assert.Equal("nota da parada", updated.Stops[0].Notes);
    }
}
