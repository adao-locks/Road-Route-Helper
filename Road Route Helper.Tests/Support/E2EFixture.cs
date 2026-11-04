using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace Road_Route_Helper.Tests.Support;

/// <summary>Real app on a local Kestrel port plus one headless Chromium, shared by the E2E tests.</summary>
public class E2EFixture : IAsyncLifetime
{
    private readonly ConcurrentDictionary<IPage, List<string>> _errors = new();
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public AppFactory Factory { get; } = new();
    public string BaseUrl { get; private set; } = string.Empty;
    public HttpClient Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory.UseKestrel(0);
        Factory.StartServer();
        BaseUrl = Factory.ClientOptions.BaseAddress.ToString().TrimEnd('/');
        Api = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> NewPageAsync(BrowserNewContextOptions? options = null)
    {
        var context = await _browser.NewContextAsync(options);
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(10_000);
        var errors = new List<string>();
        _errors[page] = errors;
        page.PageError += (_, message) => errors.Add(message);
        await page.RouteAsync("**/*.tile.openstreetmap.org/**", route => route.AbortAsync());
        return page;
    }

    public IReadOnlyList<string> ScriptErrors(IPage page) => _errors[page];

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
        Api.Dispose();
        await Factory.DisposeAsync();
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<E2EFixture>;
