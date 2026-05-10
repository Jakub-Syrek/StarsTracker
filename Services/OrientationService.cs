using Microsoft.Maui.Devices.Sensors;

namespace StarsTracker.Services;

/// <summary>
/// Wraps MAUI sensor APIs to provide the device's current pointing direction
/// as Azimuth (compass bearing, 0=North) and Altitude (pitch above horizon).
/// Uses IOrientationSensor (quaternion-based) with compass + accelerometer fallback.
/// </summary>
public sealed class OrientationService : IDisposable
{
    // Current device orientation
    public double AzimuthDeg { get; private set; }
    public double AltitudeDeg { get; private set; }

    private bool _useOrientationSensor;
    private bool _disposed;

    public event Action? OrientationChanged;

    public void Start()
    {
        if (_disposed) return;

        // Prefer full orientation sensor (quaternion)
        if (OrientationSensor.Default.IsSupported)
        {
            _useOrientationSensor = true;
            OrientationSensor.Default.ReadingChanged += OnOrientationChanged;
            OrientationSensor.Default.Start(SensorSpeed.UI);
        }
        else
        {
            // Fallback: compass + accelerometer
            _useOrientationSensor = false;

            if (Compass.Default.IsSupported)
            {
                Compass.Default.ReadingChanged += OnCompassChanged;
                Compass.Default.Start(SensorSpeed.UI);
            }

            if (Accelerometer.Default.IsSupported)
            {
                Accelerometer.Default.ReadingChanged += OnAccelerometerChanged;
                Accelerometer.Default.Start(SensorSpeed.UI);
            }
        }
    }

    public void Stop()
    {
        if (_useOrientationSensor && OrientationSensor.Default.IsMonitoring)
        {
            OrientationSensor.Default.ReadingChanged -= OnOrientationChanged;
            OrientationSensor.Default.Stop();
        }
        else
        {
            if (Compass.Default.IsMonitoring)
            {
                Compass.Default.ReadingChanged -= OnCompassChanged;
                Compass.Default.Stop();
            }
            if (Accelerometer.Default.IsMonitoring)
            {
                Accelerometer.Default.ReadingChanged -= OnAccelerometerChanged;
                Accelerometer.Default.Stop();
            }
        }
    }

    // --- OrientationSensor (quaternion) path ---
    private void OnOrientationChanged(object? sender, OrientationSensorChangedEventArgs e)
    {
        var q = e.Reading.Orientation;
        (AzimuthDeg, AltitudeDeg) = QuaternionToAzimuthAltitude(q.X, q.Y, q.Z, q.W);
        OrientationChanged?.Invoke();
    }

    /// <summary>
    /// Converts device orientation quaternion to azimuth (compass) and altitude (pitch).
    /// Assumes portrait mode, camera pointing out the back.
    /// </summary>
    private static (double azimuth, double altitude) QuaternionToAzimuthAltitude(
        float x, float y, float z, float w)
    {
        // Rotation matrix from quaternion
        double m00 = 1 - 2 * (y * y + z * z);
        double m01 = 2 * (x * y - z * w);
        double m10 = 2 * (x * y + z * w);
        double m11 = 1 - 2 * (x * x + z * z);
        double m20 = 2 * (x * z + y * w);
        double m21 = 2 * (y * z - x * w);
        double m22 = 1 - 2 * (x * x + y * y);

        // Azimuth: bearing of the phone's "up" direction projected onto horizontal plane
        double azimuthRad = Math.Atan2(m01, m00); // yaw
        double azimuthDeg = azimuthRad * 180.0 / Math.PI;
        if (azimuthDeg < 0) azimuthDeg += 360.0;

        // Altitude: pitch of the camera (0 = horizontal, 90 = pointing straight up)
        // When camera points to sky, pitch is ~90; when at horizon, ~0.
        double altitudeRad = Math.Asin(Math.Clamp(m21, -1.0, 1.0));
        double altitudeDeg = altitudeRad * 180.0 / Math.PI;

        return (azimuthDeg, altitudeDeg);
    }

    // --- Fallback: compass + accelerometer ---
    private double _compassDeg;
    private double _accelPitch;

    private void OnCompassChanged(object? sender, CompassChangedEventArgs e)
    {
        _compassDeg = e.Reading.HeadingMagneticNorth;
        UpdateFromFallback();
    }

    private void OnAccelerometerChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        var g = e.Reading.Acceleration;
        // Pitch: angle of phone from horizontal (positive = tilted up / camera to sky)
        // When flat on table face-up: pitch=90. When held upright: pitch=0.
        _accelPitch = Math.Atan2(g.Z, Math.Sqrt(g.X * g.X + g.Y * g.Y))
                     * 180.0 / Math.PI;
        UpdateFromFallback();
    }

    private void UpdateFromFallback()
    {
        AzimuthDeg = _compassDeg;
        AltitudeDeg = _accelPitch;
        OrientationChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
