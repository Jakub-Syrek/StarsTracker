namespace StarsTracker.Shared.Contracts;

/// <summary>
/// A Messier or other catalogued deep-sky object — galaxy, nebula or
/// star cluster. Returned by <c>/api/v1/deepsky</c>. Coordinates are
/// J2000.0 (the client applies precession in the same pipeline as
/// stars).
/// </summary>
/// <param name="Id">Catalog id, e.g. "M31", "M42", "M45".</param>
/// <param name="Name">Common name, e.g. "Andromeda Galaxy".</param>
/// <param name="Type">Object class — "Galaxy", "Nebula", "Open Cluster",
/// "Globular Cluster", "Planetary Nebula", "Supernova Remnant".</param>
/// <param name="RightAscensionDeg">Right ascension in degrees (J2000, 0–360).</param>
/// <param name="DeclinationDeg">Declination in degrees (-90..+90).</param>
/// <param name="Magnitude">Apparent visual magnitude.</param>
/// <param name="ApparentSizeArcmin">Largest angular extent in arc minutes
/// (some objects are quite large — M31 is ~178', M45 is ~110').</param>
public sealed record DeepSkyObjectDto(
    string Id,
    string Name,
    string Type,
    double RightAscensionDeg,
    double DeclinationDeg,
    double Magnitude,
    double ApparentSizeArcmin);
