using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Road_Route_Helper.Tests.Support;

/// <summary>Runs the real app under the pt-BR culture, whatever the machine's culture is.</summary>
public class AppFactory : WebApplicationFactory<Program>
{
    public AppFactory()
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("pt-BR");
    }
}
