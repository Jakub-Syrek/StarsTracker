namespace StarsTracker.Models;

/// <summary>
/// Represents a star from the HYG star catalogue.
/// </summary>
/// <param name="Id">Catalog identifier.</param>
/// <param name="Name">Common name (e.g. "Sirius", "Polaris").</param>
/// <param name="RA">Right Ascension in hours, J2000 epoch (0–24).</param>
/// <param name="Dec">Declination in degrees, J2000 epoch (-90..+90).</param>
/// <param name="Magnitude">Apparent visual magnitude — lower means brighter
/// (Sirius = -1.46, naked-eye limit ≈ 6.5).</param>
/// <param name="DistanceLightYears">Distance from Earth in light years when
/// known, null for stars without a confidently published distance.</param>
public sealed record Star(
    int Id,
    string Name,
    double RA,
    double Dec,
    double Magnitude,
    double? DistanceLightYears = null);
