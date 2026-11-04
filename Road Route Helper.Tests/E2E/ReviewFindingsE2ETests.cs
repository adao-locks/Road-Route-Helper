using System.Buffers.Binary;
using System.Net.Http.Json;
using Microsoft.Playwright;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class ReviewFindingsE2ETests(E2EFixture fixture)
{
    private async Task<TripDto> CreateTrip(TripDto dto)
    {
        var response = await fixture.Api.PostAsJsonAsync("/api/trips", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripDto>())!;
    }

    [Fact]
    public async Task Resposta_antiga_da_rota_nao_e_salva_com_as_paradas_novas()
    {
        var trip = await CreateTrip(TripSamples.Valid(stops: 3));
        var page = await fixture.NewPageAsync();
        var release = new TaskCompletionSource();
        var calls = await OsrmStub.HoldFirstRouteAsync(page, release.Task, heldDistanceMeters: 111_000, nextDistanceMeters: 7_000);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Edit/{trip.Id}");
        for (var i = 0; i < 100 && calls.Count == 0; i++) await Task.Delay(50);
        Assert.Equal(1, calls.Count);

        await page.Locator("#stopsList button.btn-danger").First.ClickAsync();
        await page.GetByTestId("save-trip").ClickAsync();
        release.SetResult();
        await page.WaitForURLAsync(fixture.BaseUrl + "/");

        var saved = await fixture.Api.GetFromJsonAsync<TripDto>($"/api/trips/{trip.Id}");
        Assert.Equal(2, saved!.Stops.Count);
        Assert.Equal(7, saved.TotalDistanceKm);
        Assert.Equal(2, saved.RouteGeometry.Count);
    }

    [Fact]
    public async Task Viagem_sem_paradas_exporta_png()
    {
        var trip = await CreateTrip(TripSamples.Valid(stops: 0));
        var page = await fixture.NewPageAsync(new BrowserNewContextOptions { AcceptDownloads = true });
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/View/{trip.Id}");

        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator("#exportPng").ClickAsync());

        var bytes = await File.ReadAllBytesAsync(await download.PathAsync());
        Assert.Equal(2400, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
    }
}
