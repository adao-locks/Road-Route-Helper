using System.Text.Json;
using Road_Route_Helper.Domain;
using Road_Route_Helper.Infrastructure;

namespace Road_Route_Helper.Tests;

public class JsonTripRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rrh-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "sub", "trips.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static Trip Sample(string name = "Viagem") => new()
    {
        Name = name,
        Stops = [new Stop { Name = "Parada", Latitude = -27.5, Longitude = -48.5, Category = StopCategory.Hotel }],
        RouteGeometry = [new RoutePoint { Latitude = -27.5, Longitude = -48.5 }, new RoutePoint { Latitude = -27.6, Longitude = -48.6 }],
        EstimatedDuration = TimeSpan.FromHours(25),
    };

    private void WriteRawFile(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, content);
    }

    [Fact]
    public async Task Dados_sobrevivem_a_nova_instancia()
    {
        var created = await new JsonTripRepository(FilePath).CreateAsync(Sample());
        var loaded = await new JsonTripRepository(FilePath).GetByIdAsync(created.Id);

        Assert.Equal("Viagem", loaded!.Name);
        Assert.Equal(StopCategory.Hotel, loaded.Stops[0].Category);
        Assert.Equal(2, loaded.RouteGeometry.Count);
        Assert.Equal(TimeSpan.FromHours(25), loaded.EstimatedDuration);
    }

    [Fact]
    public async Task Arquivo_ausente_inicia_vazio()
    {
        Assert.Empty(await new JsonTripRepository(FilePath).GetAllAsync());
    }

    [Fact]
    public async Task Todas_as_operacoes_funcionam()
    {
        var repo = new JsonTripRepository(FilePath);
        var created = await repo.CreateAsync(Sample());

        created.Name = "Novo nome";
        Assert.Equal("Novo nome", (await repo.UpdateAsync(created))!.Name);
        Assert.Null(await repo.UpdateAsync(new Trip { Id = Guid.NewGuid() }));

        var copy = await repo.DuplicateAsync(created.Id);
        Assert.NotEqual(created.Id, copy!.Id);
        Assert.Equal(2, (await repo.GetAllAsync()).Count());
        Assert.Null(await repo.DuplicateAsync(Guid.NewGuid()));

        Assert.True(await repo.DeleteAsync(created.Id));
        Assert.False(await repo.DeleteAsync(created.Id));
        Assert.Null(await repo.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task Arquivo_usa_schemaVersion_1()
    {
        await new JsonTripRepository(FilePath).CreateAsync(Sample());
        using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("trips").GetArrayLength());
    }

    [Fact]
    public async Task Alterar_objeto_retornado_nao_altera_o_repositorio()
    {
        var repo = new JsonTripRepository(FilePath);
        var created = await repo.CreateAsync(Sample());
        created.Name = "alterado";
        var fetched = await repo.GetByIdAsync(created.Id);
        Assert.Equal("Viagem", fetched!.Name);

        fetched.Stops[0].Name = "alterada";
        Assert.Equal("Parada", (await repo.GetByIdAsync(created.Id))!.Stops[0].Name);
    }

    [Fact]
    public async Task Gravacao_mantem_bak_da_versao_anterior()
    {
        var repo = new JsonTripRepository(FilePath);
        await repo.CreateAsync(Sample("primeira"));
        await repo.CreateAsync(Sample("segunda"));

        var backup = File.ReadAllText(FilePath + ".bak");
        Assert.Contains("primeira", backup);
        Assert.DoesNotContain("segunda", backup);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"schemaVersion":99,"trips":[]}""")]
    [InlineData("""{"trips":[]}""")]
    public void Arquivo_invalido_gera_erro_e_fica_intacto(string content)
    {
        WriteRawFile(content);
        var before = File.ReadAllBytes(FilePath);

        Assert.Throws<TripStoreException>(() => new JsonTripRepository(FilePath));

        Assert.Equal(before, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public async Task Cinquenta_gravacoes_simultaneas_nao_perdem_viagens()
    {
        var repo = new JsonTripRepository(FilePath);
        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() => repo.CreateAsync(Sample($"v{i}")))));
        Assert.Equal(50, (await new JsonTripRepository(FilePath).GetAllAsync()).Count());
    }

    [Fact]
    public async Task Nome_acentuado_sobrevive_a_recarga()
    {
        const string name = "São João & <Cia>";
        var created = await new JsonTripRepository(FilePath).CreateAsync(Sample(name));
        Assert.Equal(name, (await new JsonTripRepository(FilePath).GetByIdAsync(created.Id))!.Name);
    }

    [Fact]
    public async Task Falha_de_gravacao_nao_altera_memoria_nem_arquivo()
    {
        var repo = new JsonTripRepository(FilePath);
        await repo.CreateAsync(Sample());
        var before = File.ReadAllBytes(FilePath);

        // A directory where the temporary file goes makes the write fail on every OS.
        Directory.CreateDirectory(FilePath + ".tmp");
        await Assert.ThrowsAnyAsync<Exception>(() => repo.CreateAsync(Sample("nao salva")));

        Assert.Single(await repo.GetAllAsync());
        Assert.Equal(before, File.ReadAllBytes(FilePath));
    }
}
