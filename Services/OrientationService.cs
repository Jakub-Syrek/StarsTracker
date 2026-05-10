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

    // SLERP smoothing factor in [0, 1] applied to every incoming sample:
    // filtered = Slerp(filtered, raw, SmoothingAlpha). Lower = more damping
    // / more lag, higher = noisier / less lag. 0.2 at ~60 Hz ≈ ~50 ms time
    // constant — visually smooth but no perceptible lag.
    private const double SmoothingAlpha = 0.2;
    private bool _quaternionInitialised;

    // Manual calibration: signed offset added to sensor azimuth so that a known
    // landmark seen through the camera matches its true bearing. Persisted across
    // app restarts.
    public double AzimuthCalibrationDeg { get; private set; }
    public double AltitudeCalibrationDeg { get; private set; }

    private const string PrefAzKey = "calibration.azimuth";
    private const string PrefAltKey = "calibration.altitude";

    private bool _useOrientationSensor;
    private bool _disposed;

    public event Action? OrientationChanged;

    public OrientationService()
    {
        AzimuthCalibrationDeg = Preferences.Default.Get(PrefAzKey, 0.0);
        AltitudeCalibrationDeg = Preferences.Default.Get(PrefAltKey, 0.0);
    }

    /// <summary>Stores a new azimuth/altitude offset and persists it.</summary>
    public void SetCalibration(double azimuthOffsetDeg, double altitudeOffsetDeg)
    {
        AzimuthCalibrationDeg = NormalizeSignedDeg(azimuthOffsetDeg);
        AltitudeCalibrationDeg = Math.Clamp(altitudeOffsetDeg, -45, 45);
        Preferences.Default.Set(PrefAzKey, AzimuthCalibrationDeg);
        Preferences.Default.Set(PrefAltKey, AltitudeCalibrationDeg);
    }

    public void ClearCalibration() => SetCalibration(0, 0);

    /// <summary>Normalises an angle into (-180, 180].</summary>
    private static double NormalizeSignedDeg(double deg)
    {
        double d = ((deg % 360) + 360) % 360;
        if (d > 180) d -= 360;
        return d;
    }

    public void Start()
    {
        if (_disposed) return;

        // Prefer full orientation sensor (quaternion)
        if (OrientationSensor.Default.IsSupported)
        {
            _useOrientationSensor = true;
            OrientationSensor.Default.ReadingChanged += OnOrientationChanged;
            // Game speed is ~60 Hz on most devices — combined with SLERP smoothing
            // this gives a much steadier overlay than the default UI speed (~16 Hz).
            OrientationSensor.Default.Start(SensorSpeed.Game);
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

        if (!_quaternionInitialised)
        {
            // Seed the filter with the first sample so we don't slerp away
            // from a zeroed (0,0,0,1) initial state for the first 30+ frames.
            QuatX = q.X; QuatY = q.Y; QuatZ = q.Z; QuatW = q.W;
            _quaternionInitialised = true;
        }
        else
        {
            var (fx, fy, fz, fw) = OrientationMath.Slerp(
                QuatX, QuatY, QuatZ, QuatW,
                q.X, q.Y, q.Z, q.W,
                SmoothingAlpha);
            QuatX = (float)fx; QuatY = (float)fy; QuatZ = (float)fz; QuatW = (float)fw;
        }
        HasQuaternion = true;

        (AzimuthDeg, AltitudeDeg) = OrientationMath.AzimuthAltitude(QuatX, QuatY, QuatZ, QuatW);
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
