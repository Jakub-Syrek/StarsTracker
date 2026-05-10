namespace StarsTracker.Models;

/// <summary>
/// A geographic landmark used for manual compass calibration.
/// User aims the crosshair at the landmark and confirms — the offset
/// between the true bearing (from GPS) and the sensor's reported azimuth
/// becomes the calibration delta.
/// </summary>
public sealed record Landmark(
    string Name,
    double Latitude,
    double Longitude,
    double ElevationMeters = 0);
