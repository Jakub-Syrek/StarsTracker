namespace StarsTracker.Models;

/// <summary>
/// Represents a star from the HYG star catalogue.
/// RA (Right Ascension) is in hours (0–24).
/// Dec (Declination) is in degrees (-90 to +90).
/// Magnitude: lower = brighter (Sirius = -1.46, naked-eye limit ≈ 6.5).
/// </summary>
public sealed record Star(
    int Id,
    string Name,
    double RA,
    double Dec,
    double Magnitude);
