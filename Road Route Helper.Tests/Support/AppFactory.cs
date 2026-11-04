using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Road_Route_Helper.Tests.Support;

/// <summary>
/// Runs the real app under the pt-BR culture, whatever the machine's culture is. Unless a path is
/// given, trips live in a temporary file owned and deleted by this factory.
/// </summary>
public class AppFactory : WebApplicationFactory<Program>
{
    private readonly string _tripsFile;
    private readonly bool _ownsFile;

    public AppFactory(string? tripsFile = null)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("pt-BR");
        _ownsFile = tripsFile == null;
        _tripsFile = tripsFile ?? Path.Combine(Path.GetTempPath(), "rrh-" + Guid.NewGuid() + ".json");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("Storage:TripsFilePath", _tripsFile);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !_ownsFile) return;
        foreach (var path in new[] { _tripsFile, _tripsFile + ".bak", _tripsFile + ".tmp" })
        {
            File.Delete(path);
        }
    }
}
