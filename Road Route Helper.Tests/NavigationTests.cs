using System.Net;
using System.Text.RegularExpressions;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests;

public partial class NavigationTests
{
    [GeneratedRegex("<a class=\"nav(?:bar-brand|-link)\" href=\"([^\"]+)\"")]
    private static partial Regex NavLink();

    [Fact]
    public async Task Links_do_menu_nao_retornam_404()
    {
        using var factory = new AppFactory();
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        var links = NavLink().Matches(html).Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(links);
        foreach (var link in links)
        {
            Assert.True((await client.GetAsync(link)).IsSuccessStatusCode, link);
        }
    }

    [Fact]
    public async Task Home_Create_foi_removido()
    {
        using var factory = new AppFactory();
        var response = await factory.CreateClient().GetAsync("/Home/Create");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
