using System.Net.Http.Json;
using Microsoft.Playwright;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class RouteStateTests(E2EFixture fixture)
{
    private async Task<TripDto> CreateTrip(TripDto dto)
    {
        var response = await fixture.Api.PostAsJsonAsync("/api/trips", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripDto>())!;
    }

    private static TripDto WithGeometry(int stops)
    {
        var dto = TripSamples.Valid(stops: stops);
        dto.TotalDistanceKm = 50;
        dto.EstimatedDuration = TimeSpan.FromHours(1);
        dto.RouteGeometry =
        [
            new RoutePointDto { Latitude = -27.5, Longitude = -48.5 },
            new RoutePointDto { Latitude = -27.6, Longitude = -48.6 },
        ];
        return dto;
    }

    private async Task<IPage> OpenEditor(TripDto trip, int routeDelayMs = 0)
    {
        var page = await fixture.NewPageAsync();
        await OsrmStub.ReturnRouteAsync(page, 1000, 60, routeDelayMs);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        return page;
    }

    [Fact]
    public async Task Mudar_paradas_invalida_a_rota_anterior()
    {
        var page = await OpenEditor(await CreateTrip(WithGeometry(3)), routeDelayMs: 2000);
        await page.Locator("#stopsList button.btn-danger").First.ClickAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => window._tripCreate.route === null"));
    }

    [Fact]
    public async Task Calcular_rota_espera_o_resultado_novo()
    {
        var page = await OpenEditor(await CreateTrip(WithGeometry(2)), routeDelayMs: 800);
        var result = await page.EvaluateAsync<double[]>("""
            async () => {
                const start = performance.now();
                const ok = await window._tripCreate.calculateRoute();
                return [ok ? 1 : 0, performance.now() - start, window._tripCreate.route.distanceKm];
            }
            """);
        Assert.Equal(1, result[0]);
        Assert.True(result[1] >= 700, $"resolveu em {result[1]} ms");
        Assert.Equal(1, result[2]);
    }

    [Fact]
    public async Task Falha_no_servico_mostra_mensagem_e_permite_salvar()
    {
        var trip = await CreateTrip(TripSamples.Valid());
        var page = await fixture.NewPageAsync();
        await OsrmStub.FailAsync(page);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.GetByTestId("calc-route").ClickAsync();

        var status = page.Locator("#routeStatus");
        await status.GetByText("Não foi possível calcular a rota").WaitForAsync();
        await page.GetByTestId("save-trip").ClickAsync();
        await page.WaitForURLAsync(fixture.BaseUrl + "/");

        var saved = await fixture.Api.GetFromJsonAsync<TripDto>($"/api/trips/{trip.Id}");
        Assert.Empty(saved!.RouteGeometry);
    }

    [Fact]
    public async Task Linha_antiga_sai_do_mapa_quando_a_rota_muda()
    {
        var page = await OpenEditor(await CreateTrip(WithGeometry(3)));
        await page.EvaluateAsync("() => { window.__line = window._tripCreate.savedRouteLine; }");
        await page.Locator("#stopsList button.btn-danger").First.ClickAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => window._tripCreate.map.hasLayer(window.__line)"));
    }

    [Fact]
    public async Task Tela_de_visualizacao_enquadra_a_rota_ao_abrir()
    {
        var dto = WithGeometry(2);
        dto.RouteGeometry = [new RoutePointDto { Latitude = -27.6, Longitude = -48.5 }, new RoutePointDto { Latitude = -28.0, Longitude = -50.3 }];
        var trip = await CreateTrip(dto);
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/View/{trip.Id}");

        var inView = await page.EvaluateAsync<bool>("""
            () => {
                const bounds = map.getBounds();
                return bounds.contains([-27.6, -48.5]) && bounds.contains([-28.0, -50.3]);
            }
            """);
        Assert.True(inView);
    }

    [Fact]
    public async Task Tela_de_visualizacao_mostra_atribuicao_OSM()
    {
        var trip = await CreateTrip(WithGeometry(2));
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/View/{trip.Id}");
        Assert.Contains("OpenStreetMap", await page.Locator(".leaflet-control-attribution").InnerTextAsync());
    }

    [Fact]
    public async Task Botao_duplicar_cria_copia_e_abre_a_pagina()
    {
        var trip = await CreateTrip(WithGeometry(2));
        var page = await fixture.NewPageAsync();
        var original = $"{fixture.BaseUrl}/Trips/View/{trip.Id}";
        await page.GotoAsync(original);
        await page.Locator("#duplicateTrip").ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/Trips/View/") && url != original);
        Assert.Empty(fixture.ScriptErrors(page));
    }

    [Fact]
    public async Task Tela_de_criacao_nao_tem_botao_de_poster()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        Assert.Equal(0, await page.Locator("[onclick*=generateImage], :text('Generate Route Image')").CountAsync());
    }
}
