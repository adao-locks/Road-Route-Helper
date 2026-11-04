using System.Net.Http.Json;
using Microsoft.Playwright;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Domain;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class TripEditorTests(E2EFixture fixture)
{
    private async Task<TripDto> CreateTrip(TripDto dto)
    {
        var response = await fixture.Api.PostAsJsonAsync("/api/trips", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripDto>())!;
    }

    private async Task<int> TripCount() =>
        (await fixture.Api.GetFromJsonAsync<List<TripDto>>("/api/trips"))!.Count;

    private static TripDto WithRoute(TripDto dto)
    {
        dto.TotalDistanceKm = 123.45;
        dto.EstimatedDuration = TimeSpan.FromHours(1);
        dto.RouteGeometry =
        [
            new RoutePointDto { Latitude = -27.5, Longitude = -48.5 },
            new RoutePointDto { Latitude = -27.6, Longitude = -48.6 },
        ];
        return dto;
    }

    private async Task SaveAndWaitForDashboard(IPage page)
    {
        await page.GetByTestId("save-trip").ClickAsync();
        await page.WaitForURLAsync(fixture.BaseUrl + "/");
    }

    [Fact]
    public async Task Edicao_com_distancia_decimal_abre_sem_erro_de_script()
    {
        var trip = await CreateTrip(WithRoute(TripSamples.Valid()));
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Empty(fixture.ScriptErrors(page));
    }

    [Fact]
    public async Task Salvar_edicao_nao_cria_outra_viagem()
    {
        var trip = await CreateTrip(WithRoute(TripSamples.Valid()));
        var before = await TripCount();
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.Locator("#name").FillAsync("Nome novo");
        await SaveAndWaitForDashboard(page);

        Assert.Equal(before, await TripCount());
        var saved = await fixture.Api.GetFromJsonAsync<TripDto>($"/api/trips/{trip.Id}");
        Assert.Equal("Nome novo", saved!.Name);
    }

    [Fact]
    public async Task Editar_e_salvar_mantem_categoria_e_notas()
    {
        var dto = WithRoute(TripSamples.Valid());
        dto.Notes = "notas da viagem";
        dto.Stops[0].Category = StopCategory.Hotel;
        dto.Stops[0].Notes = "nota";
        var trip = await CreateTrip(dto);
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await SaveAndWaitForDashboard(page);

        var saved = await fixture.Api.GetFromJsonAsync<TripDto>($"/api/trips/{trip.Id}");
        Assert.Equal("notas da viagem", saved!.Notes);
        Assert.Equal(StopCategory.Hotel, saved.Stops[0].Category);
        Assert.Equal("nota", saved.Stops[0].Notes);
    }

    [Fact]
    public async Task Rota_de_25h_e_salva()
    {
        var trip = await CreateTrip(TripSamples.Valid());
        var page = await fixture.NewPageAsync();
        await OsrmStub.ReturnRouteAsync(page, 2_000_000, 90_000);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.GetByTestId("calc-route").ClickAsync();
        await page.WaitForFunctionAsync("() => window._tripCreate.route && window._tripCreate.route.geometry.length > 0");
        await SaveAndWaitForDashboard(page);

        var saved = await fixture.Api.GetFromJsonAsync<TripDto>($"/api/trips/{trip.Id}");
        Assert.Equal(TimeSpan.FromHours(25), saved!.EstimatedDuration);
        Assert.Equal(2000, saved.TotalDistanceKm);
    }

    [Fact]
    public async Task Tela_de_visualizacao_mostra_25h()
    {
        var dto = TripSamples.Valid();
        dto.EstimatedDuration = TimeSpan.FromHours(25);
        var trip = await CreateTrip(dto);
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/View/{trip.Id}");
        Assert.Contains("25h 00min", await page.Locator("body").InnerTextAsync());
    }

    [Fact]
    public async Task Nome_vazio_mostra_erro_na_tela()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        await page.GetByTestId("save-trip").ClickAsync();

        var error = page.Locator("#saveError");
        await error.WaitForAsync();
        Assert.Contains("Informe o nome da viagem.", await error.InnerTextAsync());
        Assert.EndsWith("/Trips/Create", page.Url);
    }
}
