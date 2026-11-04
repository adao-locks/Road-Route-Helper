using System.Buffers.Binary;
using System.Net.Http.Json;
using Microsoft.Playwright;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class PosterPngTests(E2EFixture fixture)
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private async Task<IPage> OpenTrip(string name)
    {
        var response = await fixture.Api.PostAsJsonAsync("/api/trips", TripSamples.Valid(name: name));
        var trip = await response.Content.ReadFromJsonAsync<TripDto>();
        var page = await fixture.NewPageAsync(new BrowserNewContextOptions { AcceptDownloads = true });
        await OsrmStub.ReturnRouteAsync(page, 1000, 60);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/View/{trip!.Id}");
        return page;
    }

    private static Task<IDownload> ClickExport(IPage page) =>
        page.RunAndWaitForDownloadAsync(() => page.Locator("#exportPng").ClickAsync());

    [Fact]
    public async Task Botao_baixa_png_de_2400_por_1600()
    {
        var page = await OpenTrip("Poster");
        var download = await ClickExport(page);

        var bytes = await File.ReadAllBytesAsync(await download.PathAsync());
        Assert.Equal(PngSignature, bytes[..8]);
        Assert.Equal(2400, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(1600, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }

    [Fact]
    public async Task Nome_do_arquivo_usa_o_slug()
    {
        var page = await OpenTrip("São João & <Cia>");
        var download = await ClickExport(page);
        Assert.Equal("sao-joao-cia.png", download.SuggestedFilename);
    }

    [Fact]
    public async Task Falha_ao_buscar_svg_mostra_erro()
    {
        var page = await OpenTrip("Falha");
        await page.RouteAsync("**/poster.svg", route => route.FulfillAsync(new RouteFulfillOptions { Status = 500 }));
        await page.Locator("#exportPng").ClickAsync();
        await page.Locator("#exportError").GetByText("Não foi possível gerar o PNG.").WaitForAsync();
    }
}
