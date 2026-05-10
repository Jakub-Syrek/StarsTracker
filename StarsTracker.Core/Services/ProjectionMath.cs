namespace StarsTracker.Services;

/// <summary>
/// Pinhole-camera projection math for the AR star overlay. Pure functions —
/// no MAUI / platform dependencies — so it can be unit-tested cheaply.
/// </summary>
public static class ProjectionMath
{
    /// <summary>
    /// Pinhole focal length (in pixels) for a given horizontal field of view
    /// and screen width.
    /// </summary>
    public static double FocalLengthPx(double screenWidthPx, double horizontalFovDeg)
    {
        if (screenWidthPx <= 0) throw new ArgumentOutOfRangeException(nameof(screenWidthPx));
        if (horizontalFovDeg <= 0 || horizontalFovDeg >= 180)
            throw new ArgumentOutOfRangeException(nameof(horizontalFovDeg));
        return (screenWidthPx / 2.0) / Math.Tan(horizontalFovDeg * Math.PI / 360.0);
    }

    /// <summary>
    /// Converts a horizontal-coordinate direction (azimuth, altitude in degrees)
    /// to a unit vector in the world frame (X=East, Y=North, Z=Up).
    /// </summary>
    public static (double e, double n, double u) AzAltToWorldVector(double azDeg, double altDeg)
    {
        double azRad = azDeg * Math.PI / 180.0;
        double altRad = altDeg * Math.PI / 180.0;
        double cosAlt = Math.Cos(altRad);
        return (
            cosAlt * Math.Sin(azRad),
            cosAlt * Math.Cos(azRad),
            Math.Sin(altRad));
    }

    /// <summary>
    /// Projects a device-frame direction (right, top, out-of-screen-front) onto
    /// the screen. Returns null when the direction points behind the camera
    /// (camera looks along -Z device, so dz must be negative for visible points).
    /// </summary>
    public static (double sx, double sy)? Project(
        double dx, double dy, double dz,
        double cx, double cy,
        double focalPx)
    {
        if (dz >= 0) return null; // behind the camera
        double sx = cx + focalPx * (dx / -dz);
        double sy = cy - focalPx * (dy / -dz); // screen Y grows downward
        return (sx, sy);
    }
}
