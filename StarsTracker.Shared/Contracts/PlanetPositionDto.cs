namespace StarsTracker.Shared.Contracts;

/// <summary>
/// Geocentric apparent position of a Solar-System body at a given UTC moment.
/// Returned by <c>/api/v1/planets</c>. The client converts (RA, Dec) to
/// horizontal coordinates using the same astronomy pipeline as the star
/// overlay, so projection, calibration and refraction are consistent.
/// </summary>
/// <param name="Name">Body name (e.g. "Sun", "Moon", "Mars").</param>
/// <param name="RightAscensionDeg">Right ascension in degrees (0–360).</param>
/// <param name="DeclinationDeg">Declination in degrees (-90..+90).</param>
/// <param name="ApparentMagnitude">Visual magnitude, smaller = brighter.</param>
/// <param name="DistanceAu">Distance from Earth in astronomical units.</param>
/// <param name="PhaseFraction">
/// Fraction of disc illuminated, 0–1. Only meaningful for Moon, Venus and
/// Mercury; null for outer planets and the Sun.
/// </param>
public sealed record PlanetPositionDto(
    string Name,
    double RightAscensionDeg,
    double DeclinationDeg,
    double ApparentMagnitude,
    double DistanceAu,
    double? PhaseFraction);
