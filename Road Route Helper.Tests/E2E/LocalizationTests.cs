using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public partial class LocalizationTests(E2EFixture fixture)
{
    [GeneratedRegex(@"\b(Dashboard|Create Trip|Trip Name|Description|Start Date|End Date|Route Map|Edit|Delete|Open|Stops|Back|Duplicate|Details|Distance|Duration|Generate|Del|Add)\b")]
    private static partial Regex EnglishText();

    private async Task<TripDto> CreateTrip(string json)
    {
        var response = await fixture.Api.PostAsync("/api/trips", new StringContent(json, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripDto>())!;
    }

    private async Task<TripDto> TwoStopTrip()
    {
        var dto = TripSamples.Valid(name: "Roteiro");
        dto.TotalDistanceKm = 123.46;
        dto.EstimatedDuration = TimeSpan.FromHours(2);
        dto.RouteGeometry =
        [
            new RoutePointDto { Latitude = -27.5, Longitude = -48.5 },
            new RoutePointDto { Latitude = -27.6, Longitude = -48.6 },
        ];
        var response = await fixture.Api.PostAsJsonAsync("/api/trips", dto);
        return (await response.Content.ReadFromJsonAsync<TripDto>())!;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Trips/Create")]
    public async Task Paginas_tem_lang_ptBR(string path)
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + path);
        Assert.Equal("pt-BR", await page.Locator("html").GetAttributeAsync("lang"));
    }

    [Fact]
    public async Task Paginas_nao_tem_textos_antigos_em_ingles()
    {
        var trip = await TwoStopTrip();
        var page = await fixture.NewPageAsync();
        foreach (var path in new[] { "/Trips/Create", $"/Trips/Edit/{trip.Id}", $"/Trips/View/{trip.Id}" })
        {
            await page.GotoAsync(fixture.BaseUrl + path);
            var text = await page.Locator("body").InnerTextAsync();
            var match = EnglishText().Match(text);
            Assert.False(match.Success, $"{path}: '{match.Value}'");
        }

        await page.GotoAsync(fixture.BaseUrl + "/");
        var card = page.Locator(".card", new PageLocatorOptions { HasText = "Roteiro" }).First;
        await card.WaitForAsync();
        Assert.DoesNotMatch(EnglishText(), await card.InnerTextAsync());
        Assert.DoesNotMatch(EnglishText(), await page.Locator("nav").InnerTextAsync());
    }

    [Fact]
    public async Task Data_nao_volta_um_dia_no_fuso_do_Brasil()
    {
        var name = "Data " + Guid.NewGuid();
        await CreateTrip($$"""{"name":"{{name}}","startDate":"2026-11-04T00:00:00+00:00","stops":[]}""");
        var page = await fixture.NewPageAsync(new BrowserNewContextOptions { TimezoneId = "America/Sao_Paulo" });
        await page.GotoAsync(fixture.BaseUrl + "/");
        var card = page.Locator(".card", new PageLocatorOptions { HasText = name });
        await card.WaitForAsync();
        Assert.Contains("04/11/2026", await card.InnerTextAsync());
    }

    [Fact]
    public async Task Data_e_igual_apos_editar_e_salvar()
    {
        var name = "Datas " + Guid.NewGuid();
        var page = await fixture.NewPageAsync(new BrowserNewContextOptions { TimezoneId = "America/Sao_Paulo" });
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        await page.Locator("#name").FillAsync(name);
        await page.Locator("#startDate").FillAsync("2026-11-04");
        await page.GetByTestId("save-trip").ClickAsync();
        await page.WaitForURLAsync(fixture.BaseUrl + "/");

        var trips = await fixture.Api.GetFromJsonAsync<List<TripDto>>("/api/trips");
        var trip = trips!.Single(t => t.Name == name);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        Assert.Equal("2026-11-04", await page.Locator("#startDate").InputValueAsync());
        await page.GetByTestId("save-trip").ClickAsync();
        await page.WaitForURLAsync(fixture.BaseUrl + "/");

        await page.GotoAsync($"{fixture.BaseUrl}/Trips/View/{trip.Id}");
        Assert.Contains("04/11/2026", await page.Locator("body").InnerTextAsync());
    }

    [Fact]
    public async Task Distancia_usa_formato_brasileiro()
    {
        var trip = await TwoStopTrip();
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/View/{trip.Id}");
        Assert.Contains("123,5 km", await page.Locator("body").InnerTextAsync());
    }

    [Fact]
    public async Task Mapa_novo_abre_centralizado_no_Brasil()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        var view = await page.EvaluateAsync<double[]>("() => { const m = window._tripCreate.map; const c = m.getCenter(); return [c.lat, c.lng, m.getZoom()]; }");
        Assert.Equal(-14.2350, view[0], 2);
        Assert.Equal(-51.9253, view[1], 2);
        Assert.Equal(4, view[2]);
    }

    [Fact]
    public async Task Duplicar_usa_sufixo_Copia()
    {
        var trip = await TwoStopTrip();
        var response = await fixture.Api.PostAsync($"/api/trips/{trip.Id}/duplicate", null);
        var copy = await response.Content.ReadFromJsonAsync<TripDto>();
        Assert.Equal("Roteiro (Cópia)", copy!.Name);
    }
}
