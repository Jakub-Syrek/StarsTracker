using StarsTracker.Models;

namespace StarsTracker.Services;

/// <summary>
/// Static catalog of well-known landmarks used for compass calibration.
/// Coordinates are approximate (within a few metres) — accurate enough since
/// the bearing changes by &lt;0.1° per metre at typical landmark distances.
/// </summary>
public sealed class LandmarkService
{
    public IReadOnlyList<Landmark> Landmarks { get; } =
    [
        new("Kopiec Kościuszki",   50.0547, 19.8806, 333),
        new("Kopiec Piłsudskiego", 50.0547, 19.8580, 380),
        new("Kopiec Krakusa",      50.0445, 19.9594, 270),
        new("Kopiec Wandy",        50.0860, 20.0530, 230),
        new("Wawel",               50.0544, 19.9355, 230),
        new("Bazylika Mariacka",   50.0617, 19.9394, 280),
        new("Zakrzówek",           50.0427, 19.9049, 220),
        new("Skałki Twardowskiego",50.0420, 19.8970, 230),
    ];
}
