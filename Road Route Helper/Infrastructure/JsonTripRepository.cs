using System.Text.Json;
using Road_Route_Helper.Application.Interfaces;
using Road_Route_Helper.Domain;

namespace Road_Route_Helper.Infrastructure;

/// <summary>
/// Stores all trips in one JSON file, for a single app process. A stopgap until the real database:
/// the whole file is rewritten on every change.
/// </summary>
public class JsonTripRepository : ITripRepository
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Never mutated after being published: each change builds a new list and swaps it in once saved.
    private volatile List<Trip> _trips;

    public JsonTripRepository(string filePath)
    {
        _filePath = filePath;
        _trips = Load();
    }

    public Task<IEnumerable<Trip>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Trip>>(_trips.Select(t => t.DeepClone()).ToList());

    public Task<Trip?> GetByIdAsync(Guid id) =>
        Task.FromResult(_trips.Find(t => t.Id == id)?.DeepClone());

    public Task<Trip> CreateAsync(Trip trip)
    {
        var created = trip.DeepClone();
        created.Id = Guid.NewGuid();
        return AddAsync(created);
    }

    public async Task<Trip?> UpdateAsync(Trip trip)
    {
        await _writeLock.WaitAsync();
        try
        {
            var index = _trips.FindIndex(t => t.Id == trip.Id);
            if (index < 0) return null;
            var next = new List<Trip>(_trips) { [index] = trip.DeepClone() };
            await PublishAsync(next);
            return trip.DeepClone();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _writeLock.WaitAsync();
        try
        {
            var next = _trips.Where(t => t.Id != id).ToList();
            if (next.Count == _trips.Count) return false;
            await PublishAsync(next);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<Trip?> DuplicateAsync(Guid id)
    {
        var original = _trips.Find(t => t.Id == id);
        return original == null ? null : await AddAsync(original.ToCopy());
    }

    private async Task<Trip> AddAsync(Trip trip)
    {
        await _writeLock.WaitAsync();
        try
        {
            await PublishAsync([.. _trips, trip]);
            return trip.DeepClone();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Saves first and updates memory only after that, so a failed write changes nothing.</summary>
    private async Task PublishAsync(List<Trip> next)
    {
        var temporary = _filePath + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new TripFile(SchemaVersion, next), SerializerOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes);
            if (File.Exists(_filePath))
            {
                File.Replace(temporary, _filePath, _filePath + ".bak");
            }
            else
            {
                File.Move(temporary, _filePath);
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        _trips = next;
    }

    private List<Trip> Load()
    {
        if (!File.Exists(_filePath)) return [];

        TripFile? file;
        try
        {
            file = JsonSerializer.Deserialize<TripFile>(File.ReadAllBytes(_filePath), SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new TripStoreException($"O arquivo de viagens '{_filePath}' está corrompido. Corrija-o ou restaure o '.bak'.", ex);
        }

        if (file?.Trips == null || file.SchemaVersion != SchemaVersion)
        {
            throw new TripStoreException($"O arquivo de viagens '{_filePath}' não tem o formato esperado (schemaVersion {SchemaVersion}).");
        }
        return file.Trips;
    }

    private sealed record TripFile(int SchemaVersion, List<Trip>? Trips);
}
