using System.Text.Json;
using System.Text.Json.Serialization;
using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

/// <summary>
/// Loads Resources/stars-extended.json into memory at startup and exposes
/// the entries via <see cref="IExtendedStarCatalog"/>. The JSON is generated
/// off-line by <c>scripts/generate_extended_stars.py</c> from the HYG
/// database — see that file for the exact filter (mag ≤ 6.0).
/// </summary>
public sealed class ExtendedStarCatalog : IExtendedStarCatalog
{
    public IReadOnlyList<StarRecordDto> All { get; }

    public ExtendedStarCatalog(IHostEnvironment env)
    {
        string path = Path.Combine(env.ContentRootPath, "Resources", "stars-extended.json");
        if (!File.Exists(path))
        {
            All = [];
            return;
        }

        using var stream = File.OpenRead(path);
        All = JsonSerializer.Deserialize(stream, ExtendedStarsJsonContext.Default.StarFileRecordArray)
                ?.Select(r => new StarRecordDto(r.Id, r.Name, r.Ra, r.Dec, r.Mag, r.DistLy))
                .ToList()
                .AsReadOnly()
              ?? [];
    }
}

// File-format DTO mirroring the JSON shape verbatim. Internal so the source
// generator can target it.
internal sealed record StarFileRecord(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ra")] double Ra,
    [property: JsonPropertyName("dec")] double Dec,
    [property: JsonPropertyName("mag")] double Mag,
    [property: JsonPropertyName("dist_ly")] double? DistLy = null);

[JsonSerializable(typeof(StarFileRecord[]))]
internal sealed partial class ExtendedStarsJsonContext : JsonSerializerContext { }
