using System.Text.Json;
using System.Text.Json.Serialization;
using StarsTracker.Models;

namespace StarsTracker.Services;

public sealed class StarCatalogService
{
    private IReadOnlyList<Star>? _cache;

    /// <summary>
    /// Loads and caches the star catalogue from the embedded assets.
    /// Returns stars brighter than magnitude 5.0 (visible to naked eye).
    /// </summary>
    public async Task<IReadOnlyList<Star>> GetVisibleStarsAsync()
    {
        if (_cache is not null)
            return _cache;

        await using var stream = await FileSystem.OpenAppPackageFileAsync("stars.json");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        var dtos = JsonSerializer.Deserialize(json, StarJsonContext.Default.StarDtoArray)
                   ?? [];

        _cache = dtos
            .Where(s => s.Mag <= 5.0)
            .Select(s => new Star(
                s.Id,
                s.Name,
                s.Ra,
                s.Dec,
                s.Mag,
                StarDistances.Get(s.Name)))
            .OrderBy(s => s.Magnitude)
            .ToList()
            .AsReadOnly();

        return _cache;
    }
}

// DTO matching the JSON shape — must be internal (not file) so the JSON source generator can reference it
internal sealed record StarDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ra")] double Ra,
    [property: JsonPropertyName("dec")] double Dec,
    [property: JsonPropertyName("mag")] double Mag);

[JsonSerializable(typeof(StarDto[]))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal sealed partial class StarJsonContext : JsonSerializerContext { }
