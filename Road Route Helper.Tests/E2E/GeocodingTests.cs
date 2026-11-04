using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public partial class GeocodingTests(E2EFixture fixture)
{
    [GeneratedRegex(@"/search\?")]
    private static partial Regex SearchUrl();

    private static readonly object[] Florianopolis =
    [
        new { lat = "-27.5954", lon = "-48.5480", display_name = "Florianópolis, Santa Catarina, Brasil" },
    ];

    private static Dictionary<string, string> Cors => new() { ["Access-Control-Allow-Origin"] = "*" };

    private async Task<IPage> OpenCreatePage(Func<IRoute, Task> handler)
    {
        var page = await fixture.NewPageAsync();
        await page.RouteAsync(SearchUrl(), handler);
        await page.GotoAsync($"{fixture.BaseUrl}/Trips/Create");
        return page;
    }

    private static Task Respond(IRoute route, object body) => route.FulfillAsync(new RouteFulfillOptions
    {
        ContentType = "application/json",
        Headers = Cors,
        Body = JsonSerializer.Serialize(body),
    });

    private static async Task Search(IPage page, string text)
    {
        await page.Locator("#addressQuery").FillAsync(text);
        await page.Locator("#addressSearch button[type=submit]").ClickAsync();
    }

    [Fact]
    public async Task Escolher_resultado_centraliza_e_abre_formulario()
    {
        var page = await OpenCreatePage(route => Respond(route, Florianopolis));
        await Search(page, "Florianópolis");
        await page.Locator("#addressResults button").First.ClickAsync();

        Assert.Equal("Florianópolis", await page.Locator("#stopFormName").InputValueAsync());
        var center = await page.EvaluateAsync<double[]>("() => { const c = window._tripCreate.map.getCenter(); return [c.lat, c.lng]; }");
        Assert.InRange(center[0], -27.7, -27.5);
        Assert.InRange(center[1], -48.7, -48.4);
    }

    [Fact]
    public async Task Consulta_e_enviada_codificada_ao_servico()
    {
        string? url = null;
        var page = await OpenCreatePage(route =>
        {
            url = route.Request.Url;
            return Respond(route, Array.Empty<object>());
        });
        await Search(page, "São João & Cia");
        await page.Locator("#addressStatus").GetByText("Nenhum endereço").WaitForAsync();

        Assert.Contains("format=jsonv2", url);
        Assert.Contains("limit=5", url);
        Assert.Contains("q=S%C3%A3o%20Jo%C3%A3o%20%26%20Cia", url);
    }

    [Fact]
    public async Task Busca_so_roda_ao_enviar()
    {
        var calls = 0;
        var page = await OpenCreatePage(route =>
        {
            calls++;
            return Respond(route, Florianopolis);
        });
        await page.Locator("#addressQuery").PressSequentiallyAsync("Floripa");
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Duas_buscas_seguidas_respeitam_um_segundo()
    {
        var times = new List<DateTime>();
        var page = await OpenCreatePage(route =>
        {
            times.Add(DateTime.UtcNow);
            return Respond(route, Florianopolis);
        });
        await Search(page, "um");
        await page.Locator("#addressResults button").First.WaitForAsync();
        await Search(page, "dois");
        await page.WaitForFunctionAsync("() => !document.querySelector('#addressSearch button[type=submit]').disabled");

        Assert.Equal(2, times.Count);
        Assert.True(times[1] - times[0] >= TimeSpan.FromMilliseconds(950), $"intervalo {times[1] - times[0]}");
    }

    [Fact]
    public async Task Sem_resultados_mostra_mensagem()
    {
        var page = await OpenCreatePage(route => Respond(route, Array.Empty<object>()));
        await Search(page, "xyz");
        await page.Locator("#addressStatus").GetByText("Nenhum endereço encontrado.").WaitForAsync();
    }

    [Fact]
    public async Task Erro_do_servico_mostra_mensagem_e_mapa_segue_usavel()
    {
        var page = await OpenCreatePage(route => route.FulfillAsync(new RouteFulfillOptions { Status = 500, Headers = Cors, Body = "{}" }));
        await Search(page, "xyz");
        await page.Locator("#addressStatus").GetByText("Não foi possível buscar o endereço.").WaitForAsync();

        await page.Locator("#map").ClickAsync();
        await page.Locator("#stopFormName").WaitForAsync();
    }

    [Fact]
    public async Task Nome_de_resultado_com_html_aparece_como_texto()
    {
        const string payload = "<img src=x onerror=\"window.__xss=1\">";
        var page = await OpenCreatePage(route => Respond(route, new object[] { new { lat = "1", lon = "1", display_name = payload } }));
        await Search(page, "xyz");
        await page.Locator("#addressResults").GetByText(payload).WaitForAsync();
        await page.WaitForTimeoutAsync(300);
        Assert.False(await page.EvaluateAsync<bool>("() => window.__xss === 1"));
    }

    [Fact]
    public async Task Pagina_exibe_atribuicao_da_busca()
    {
        var page = await OpenCreatePage(route => Respond(route, Florianopolis));
        Assert.Contains("OpenStreetMap", await page.Locator("#addressSearch").InnerTextAsync());
    }
}
