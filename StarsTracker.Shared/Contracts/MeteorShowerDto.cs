namespace StarsTracker.Shared.Contracts;

/// <summary>
/// A meteor shower currently active around the requested UTC. Returned by
/// <c>/api/v1/meteorshowers</c> as the subset of the curated catalogue
/// whose activity window overlaps "now ± 7 days".
/// </summary>
/// <param name="Code">IMO three-letter code, e.g. "PER" (Perseids).</param>
/// <param name="Name">Common name, e.g. "Perseids".</param>
/// <param name="PeakUtc">UTC moment of maximum activity.</param>
/// <param name="ZenithalHourlyRate">Approximate ZHR at peak.</param>
/// <param name="RadiantRightAscensionDeg">Radiant RA (degrees, J2000).</param>
/// <param name="RadiantDeclinationDeg">Radiant Dec (degrees, J2000).</param>
/// <param name="DaysUntilPeak">Signed offset — negative if the peak has
/// passed, positive if upcoming. The client can render an "ACTIVE" badge
/// when this is within ±2 days.</param>
public sealed record MeteorShowerDto(
    string Code,
    string Name,
    DateTime PeakUtc,
    int ZenithalHourlyRate,
    double RadiantRightAscensionDeg,
    double RadiantDeclinationDeg,
    int DaysUntilPeak);
