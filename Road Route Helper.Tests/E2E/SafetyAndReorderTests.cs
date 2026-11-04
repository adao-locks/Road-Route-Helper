using System.Net.Http.Json;
using Microsoft.Playwright;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class SafetyAndReorderTests(E2EFixture fixture)
{
    private const string Payload = "<img src=x onerror=\"window.__xss=1\">";

    private async Task<TripDto> CreateTrip(TripDto dto)
    {
        var response = await fixture.Api.PostAsJsonAsync("/api/trips", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripDto>())!;
    }

    private static async Task AssertNoXss(IPage page)
    {
        await page.WaitForTimeoutAsync(500);
        Assert.False(await page.EvaluateAsync<bool>("() => window.__xss === 1"));
    }

    [Fact]
    public async Task Dashboard_nao_executa_script_no_nome()
    {
        await CreateTrip(TripSamples.Valid(name: Payload));
        var page = await fixture.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/");
        await page.GetByText(Payload).First.WaitForAsync();
        await AssertNoXss(page);
    }

    [Fact]
    public async Task Lista_de_paradas_nao_executa_script_no_nome()
    {
        var dto = TripSamples.Valid();
        dto.Stops[0].Name = Payload;
        var trip = await CreateTrip(dto);
        var page = await fixture.NewPageAsync();
        await OsrmStub.ReturnRouteAsync(page, 1000, 60);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        await page.Locator("#stopsList").GetByText(Payload).WaitForAsync();
        await AssertNoXss(page);
    }

    [Fact]
    public async Task Popup_do_mapa_nao_executa_script_no_nome()
    {
        var dto = TripSamples.Valid();
        dto.Stops[0].Name = Payload;
        var trip = await CreateTrip(dto);
        var page = await fixture.NewPageAsync();
        await OsrmStub.ReturnRouteAsync(page, 1000, 60);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/View/{trip.Id}");
        await page.Locator(".leaflet-marker-icon").First.ClickAsync(new LocatorClickOptions { Force = true });
        await page.Locator(".leaflet-popup-content").GetByText(Payload).WaitForAsync();
        await AssertNoXss(page);
    }

    [Fact]
    public async Task Nome_acentuado_aparece_correto_no_dashboard()
    {
        const string name = "São João & <Cia>";
        await CreateTrip(TripSamples.Valid(name: name));
        var page = await fixture.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/");
        await page.GetByText(name).First.WaitForAsync();
    }

    [Fact]
    public async Task Reordenar_paradas_de_mesmo_nome_mantem_as_tres()
    {
        var dto = TripSamples.Valid(stops: 3);
        dto.Stops.ForEach(s => s.Name = "Stop");
        var trip = await CreateTrip(dto);
        var page = await fixture.NewPageAsync();
        await OsrmStub.ReturnRouteAsync(page, 1000, 60);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");

        var ids = await page.EvaluateAsync<string[]>("""
            () => {
                const before = window._tripCreate.stops.map(s => s.id);
                const ul = document.getElementById('stopsList');
                ul.insertBefore(ul.children[2], ul.children[0]);
                Sortable.get(ul).options.onEnd({});
                return window._tripCreate.stops.map(s => s.id).concat(before);
            }
            """);

        var after = ids[..3];
        var before = ids[3..];
        Assert.Equal(3, after.Distinct().Count());
        Assert.Equal(new[] { before[2], before[0], before[1] }, after);
    }
}
