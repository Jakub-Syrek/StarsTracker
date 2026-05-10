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

    // Raw quaternion (X, Y, Z, W) — Android rotation-vector convention:
    // rotates a vector from world frame (X=East, Y=North, Z=Up) to device frame
    // (X=right edge, Y=top edge, Z=out of front screen).
    public float QuatX { get; private set; }
    public float QuatY { get; private set; }
    public float QuatZ { get; private set; }
    public float QuatW { get; private set; } = 1f;
    public bool HasQuaternion { get; private set; }

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
        QuatX = q.X; QuatY = q.Y; QuatZ = q.Z; QuatW = q.W;
        HasQuaternion = true;

        // Camera direction in device frame is (0, 0, -1). Apply R(q)^T to get world coords.
        // R(q) rotates world->device, so R^T rotates device->world. Camera direction in
        // world frame = R^T * (0,0,-1) = -row_2(R) = (-m20, -m21, -m22).
        double m20 = 2 * (q.X * q.Z - q.Y * q.W);
        double m21 = 2 * (q.Y * q.Z + q.X * q.W);
        double m22 = 1 - 2 * (q.X * q.X + q.Y * q.Y);

        double vEast = -m20, vNorth = -m21, vUp = -m22;
        double azRad = Math.Atan2(vEast, vNorth);
        AzimuthDeg = (azRad * 180.0 / Math.PI + 360.0) % 360.0;
        AltitudeDeg = Math.Asin(Math.Clamp(vUp, -1.0, 1.0)) * 180.0 / Math.PI;

        OrientationChanged?.Invoke();
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
