using System.Net;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests;

public class SmokeTests
{
    [Fact]
    public async Task WeatherForecast_foi_removido()
    {
        using var factory = new AppFactory();
        var response = await factory.CreateClient().GetAsync("/WeatherForecast");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Api_de_viagens_responde()
    {
        using var factory = new AppFactory();
        var response = await factory.CreateClient().GetAsync("/api/trips");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

[Collection("E2E")]
[Trait("Category", "E2E")]
public class DashboardSmokeTests(E2EFixture fixture)
{
    [Fact]
    public async Task Dashboard_abre_sem_erro_de_script()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/");
        await page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        Assert.Empty(fixture.ScriptErrors(page));
    }
}
