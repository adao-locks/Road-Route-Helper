using System.Globalization;

namespace Road_Route_Helper.Tests.Support;

public static class Culture
{
    public static IDisposable Use(string name)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        return new Restore(previous);
    }

    private sealed class Restore(CultureInfo previous) : IDisposable
    {
        public void Dispose() => CultureInfo.CurrentCulture = previous;
    }
}
