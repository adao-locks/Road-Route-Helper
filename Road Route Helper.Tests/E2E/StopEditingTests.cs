using System.Net.Http.Json;
using Microsoft.Playwright;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Domain;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class StopEditingTests(E2EFixture fixture)
{
    private async Task<TripDto> CreateTrip(TripDto dto)
    {
        var response = await fixture.Api.PostAsJsonAsync("/api/trips", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripDto>())!;
    }

    private Task<TripDto?> Saved(Guid id) => fixture.Api.GetFromJsonAsync<TripDto>($"/api/trips/{id}");

    private static TripDto WithGeometry(int stops)
    {
        var dto = TripSamples.Valid(stops: stops);
        dto.RouteGeometry =
        [
            new RoutePointDto { Latitude = -27.5, Longitude = -48.5 },
            new RoutePointDto { Latitude = -27.6, Longitude = -48.6 },
        ];
        return dto;
    }

    private static async Task FillStopForm(IPage page, string name, StopCategory category, string notes)
    {
        await page.Locator("#stopFormName").FillAsync(name);
        await page.Locator("#stopFormCategory").SelectOptionAsync(((int)category).ToString());
        await page.Locator("#stopFormNotes").FillAsync(notes);
        await page.Locator("#stopFormSave").ClickAsync();
    }

    private async Task SaveTrip(IPage page)
    {
        await page.GetByTestId("save-trip").ClickAsync();
        await page.WaitForURLAsync(fixture.BaseUrl + "/");
    }

    private async Task<TripDto> FindByName(string name)
    {
        var trips = await fixture.Api.GetFromJsonAsync<List<TripDto>>("/api/trips");
        return trips!.Single(t => t.Name == name);
    }

    [Fact]
    public async Task Clicar_no_mapa_e_salvar_cria_parada_com_os_dados()
    {
        var tripName = "Mapa " + Guid.NewGuid();
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        await page.Locator("#name").FillAsync(tripName);
        await page.Locator("#map").ClickAsync();
        await FillStopForm(page, "Pousada", StopCategory.Hotel, "reservar");

        var center = await page.EvaluateAsync<double[]>("() => { const c = window._tripCreate.map.getCenter(); return [c.lat, c.lng]; }");
        await SaveTrip(page);

        var stop = (await FindByName(tripName)).Stops.Single();
        Assert.Equal("Pousada", stop.Name);
        Assert.Equal(StopCategory.Hotel, stop.Category);
        Assert.Equal("reservar", stop.Notes);
        Assert.InRange(Math.Abs(stop.Latitude - center[0]), 0, 0.5);
        Assert.InRange(Math.Abs(stop.Longitude - center[1]), 0, 0.5);
    }

    [Fact]
    public async Task Criar_parada_nao_tem_mais_botao_no_centro()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        Assert.Equal(0, await page.Locator("#stopName, #addStopBtn").CountAsync());
    }

    [Fact]
    public async Task Editar_na_lista_altera_e_persiste_ao_salvar()
    {
        var trip = await CreateTrip(WithGeometry(2));
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.Locator("#stopsList [data-action=edit]").First.ClickAsync();
        Assert.Equal("Parada 1", await page.Locator("#stopFormName").InputValueAsync());
        await FillStopForm(page, "Renomeada", StopCategory.CoffeeShop, "nota nova");
        await SaveTrip(page);

        var saved = await Saved(trip.Id);
        Assert.Equal("Renomeada", saved!.Stops[0].Name);
        Assert.Equal(StopCategory.CoffeeShop, saved.Stops[0].Category);
        Assert.Equal("nota nova", saved.Stops[0].Notes);
        Assert.Equal(2, saved.RouteGeometry.Count);
    }

    [Fact]
    public async Task Arrastar_marcador_altera_posicao_e_recalcula()
    {
        var trip = await CreateTrip(WithGeometry(2));
        var page = await fixture.NewPageAsync();
        var calls = await OsrmStub.ReturnRouteAsync(page, 1000, 60);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.EvaluateAsync("""
            () => {
                const state = window._tripCreate;
                const marker = state.markers.get(state.stops[0].id);
                marker.setLatLng([-30, -50]);
                marker.fire('dragend');
            }
            """);
        await page.WaitForFunctionAsync("() => window._tripCreate.route !== null");
        Assert.Equal(1, calls.Count);
        await SaveTrip(page);

        var saved = await Saved(trip.Id);
        Assert.Equal(-30, saved!.Stops[0].Latitude);
        Assert.Equal(-50, saved.Stops[0].Longitude);
    }

    [Fact]
    public async Task Categoria_escolhida_chega_ao_poster()
    {
        var tripName = "Poster " + Guid.NewGuid();
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        await page.Locator("#name").FillAsync(tripName);
        await page.Locator("#map").ClickAsync();
        await FillStopForm(page, "Pousada", StopCategory.Hotel, "");
        await SaveTrip(page);

        var trip = await FindByName(tripName);
        var svg = await fixture.Api.GetStringAsync($"/Trips/View/{trip.Id}/poster.svg");
        Assert.Contains("#06b6d4", svg);
    }

    [Fact]
    public async Task Varias_mudancas_rapidas_geram_uma_chamada_de_rota()
    {
        var trip = await CreateTrip(WithGeometry(3));
        var page = await fixture.NewPageAsync();
        var calls = await OsrmStub.ReturnRouteAsync(page, 1000, 60);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.EvaluateAsync("""
            () => {
                const onEnd = Sortable.get(document.getElementById('stopsList')).options.onEnd;
                onEnd({}); onEnd({}); onEnd({});
            }
            """);
        await page.WaitForFunctionAsync("() => window._tripCreate.route !== null");
        await page.WaitForTimeoutAsync(700);
        Assert.Equal(1, calls.Count);
    }

    [Fact]
    public async Task Salvar_logo_apos_mudar_parada_espera_a_rota_nova()
    {
        var trip = await CreateTrip(WithGeometry(2));
        var page = await fixture.NewPageAsync();
        await OsrmStub.ReturnRouteAsync(page, 7000, 60);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.EvaluateAsync("() => Sortable.get(document.getElementById('stopsList')).options.onEnd({})");
        await SaveTrip(page);

        var saved = await Saved(trip.Id);
        Assert.Equal(7, saved!.TotalDistanceKm);
        Assert.NotEmpty(saved.RouteGeometry);
    }

    [Fact]
    public async Task Nome_em_branco_mostra_erro_no_formulario()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        await page.Locator("#map").ClickAsync();
        await page.Locator("#stopFormName").FillAsync("   ");
        await page.Locator("#stopFormSave").ClickAsync();

        await page.Locator("#stopFormError").GetByText("Informe o nome da parada.").WaitForAsync();
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window._tripCreate.stops.length"));
    }
}
