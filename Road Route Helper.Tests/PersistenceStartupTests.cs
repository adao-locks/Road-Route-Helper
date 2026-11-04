using System.Net.Http.Json;
using Road_Route_Helper.Application.DTOs;
using Road_Route_Helper.Tests.Support;

namespace Road_Route_Helper.Tests;

public class PersistenceStartupTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "rrh-" + Guid.NewGuid() + ".json");

    public void Dispose()
    {
        foreach (var path in new[] { _file, _file + ".bak", _file + ".tmp" })
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void App_nao_inicia_com_arquivo_corrompido()
    {
        File.WriteAllText(_file, "{ corrompido");
        var before = File.ReadAllBytes(_file);
        using var factory = new AppFactory(_file);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Equal(before, File.ReadAllBytes(_file));
    }

    [Fact]
    public async Task Viagem_criada_pela_API_sobrevive_a_novo_app()
    {
        Guid id;
        using (var first = new AppFactory(_file))
        {
            var response = await first.CreateClient().PostAsJsonAsync("/api/trips", TripSamples.Valid());
            id = (await response.Content.ReadFromJsonAsync<TripDto>())!.Id;
        }

        using var second = new AppFactory(_file);
        var saved = await second.CreateClient().GetFromJsonAsync<TripDto>($"/api/trips/{id}");
        Assert.Equal("Viagem", saved!.Name);
    }

    [Fact]
    public async Task App_novo_nao_tem_viagem_de_exemplo()
    {
        using var factory = new AppFactory(_file);
        Assert.Empty((await factory.CreateClient().GetFromJsonAsync<List<TripDto>>("/api/trips"))!);
    }
}
