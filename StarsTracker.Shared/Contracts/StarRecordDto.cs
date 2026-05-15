namespace StarsTracker.Shared.Contracts;

/// <summary>
/// Wire-format record for a single star in the extended catalogue served
/// by <c>/api/v1/stars/extended</c>. Mirrors the shape of the bundled
/// stars.json so the client can merge the two sources transparently.
/// </summary>
/// <param name="Id">Catalog identifier (HYG row id).</param>
/// <param name="Name">Common name, Bayer designation, Flamsteed number, or
/// HIP fallback.</param>
/// <param name="Ra">Right ascension in hours, J2000 (0–24).</param>
/// <param name="Dec">Declination in degrees, J2000 (-90..+90).</param>
/// <param name="Mag">Apparent visual magnitude.</param>
/// <param name="DistLy">Distance in light years when published; null otherwise.</param>
public sealed record StarRecordDto(
    int Id,
    string Name,
    double Ra,
    double Dec,
    double Mag,
    double? DistLy);
