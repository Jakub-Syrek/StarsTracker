using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using StarsTracker.Controls;
using StarsTracker.Models;
using StarsTracker.Services;

namespace StarsTracker.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly StarCatalogService _catalog;
    private readonly OrientationService _orientation;
    private readonly LandmarkService _landmarks;

    // Camera horizontal FOV in degrees. Initial guess for a typical smartphone
    // main rear camera; overwritten by Camera2 characteristics during preview
    // start-up via <see cref="SetCameraFov"/>.
    private double _fovHorizontalDeg = 65.0;

    // Crosshair radius for highlighting (degrees)
    private const double HighlightRadiusDeg = 5.0;

    private IReadOnlyList<Star> _allStars = [];
    private IDispatcherTimer? _refreshTimer;
    private double _screenWidth = 1;
    private double _screenHeight = 1;

    // ---- Observable properties ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private string _statusText = "Initializing...";

    public bool IsLoading => !string.IsNullOrEmpty(StatusText);

    [ObservableProperty]
    private string _highlightedStarName = string.Empty;

    [ObservableProperty]
    private bool _hasHighlightedStar;

    [ObservableProperty]
    private string _locationText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCalibration))]
    private string _calibrationStatusText = string.Empty;

    public bool HasCalibration => !string.IsNullOrEmpty(CalibrationStatusText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordingStatusText = string.Empty;

    public string RecordButtonText => IsRecording ? "Stop" : "Record";

    [ObservableProperty]
    private bool _isLandmarkPickerVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AimInstructionText))]
    [NotifyPropertyChangedFor(nameof(IsAimingMode))]
    [NotifyPropertyChangedFor(nameof(IsNotAimingMode))]
    private Landmark? _calibrationTarget;

    public string AimInstructionText => CalibrationTarget is null
        ? string.Empty
        : $"Aim crosshair at: {CalibrationTarget.Name}";

    public bool IsAimingMode => CalibrationTarget is not null;
    public bool IsNotAimingMode => CalibrationTarget is null;

    public IReadOnlyList<Landmark> Landmarks => _landmarks.Landmarks;

    // The drawable that GraphicsView renders
    public StarOverlayDrawable StarDrawable { get; } = new();

    // Invoked whenever the overlay needs a redraw
    public event Action? RedrawRequested;

    // ---- Location ----
    private double _latitudeDeg;
    private double _longitudeDeg;
    private bool _hasLocation;

    public MainViewModel(StarCatalogService catalog, OrientationService orientation, LandmarkService landmarks)
    {
        _catalog = catalog;
        _orientation = orientation;
        _landmarks = landmarks;
        _orientation.OrientationChanged += OnOrientationChanged;
        UpdateCalibrationStatusText();

        ScreenRecorder.Started += OnRecordingStarted;
        ScreenRecorder.Stopped += OnRecordingStopped;
        ScreenRecorder.Error += OnRecordingError;
    }

    private void OnRecordingStarted()
    {
        Application.Current?.Dispatcher.Dispatch(() =>
        {
            IsRecording = true;
            RecordingStatusText = "● REC";
        });
    }

    private void OnRecordingStopped(string? path)
    {
        Application.Current?.Dispatcher.Dispatch(() =>
        {
            IsRecording = false;
            RecordingStatusText = path is null
                ? string.Empty
                : $"Saved: {System.IO.Path.GetFileName(path)}";
        });
    }

    private void OnRecordingError(string message)
    {
        Application.Current?.Dispatcher.Dispatch(() =>
        {
            IsRecording = false;
            RecordingStatusText = $"Recording error: {message}";
        });
    }

    [RelayCommand]
    private void ToggleRecording()
    {
#if ANDROID
        var act = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity as StarsTracker.MainActivity;
        if (act is null) return;
        if (IsRecording) act.StopScreenRecording();
        else act.StartScreenRecording();
#endif
    }

    public async Task InitializeAsync()
    {
        StatusText = "Loading star catalog...";
        _allStars = await _catalog.GetVisibleStarsAsync();

        StatusText = "Acquiring GPS location...";
        await RequestLocationAsync();

        StatusText = "Starting sensors...";
        _orientation.Start();

        _refreshTimer = Application.Current!.Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(100);
        _refreshTimer.Tick += (_, _) => Refresh();
        _refreshTimer.Start();

        StatusText = string.Empty;
    }

    public void SetScreenSize(double width, double height)
    {
        _screenWidth = width;
        _screenHeight = height;
    }

    /// <summary>
    /// Overrides the horizontal field of view used for the pinhole projection
    /// with a value measured from the active camera's Camera2 characteristics.
    /// Clamped to a sane sanity range so that an unexpected sensor reading
    /// cannot blow up the projection.
    /// </summary>
    public void SetCameraFov(double horizontalFovDeg)
    {
        if (horizontalFovDeg < 30 || horizontalFovDeg > 120) return;
        _fovHorizontalDeg = horizontalFovDeg;
    }

    // ---- Core refresh cycle ----

    private void OnOrientationChanged() { /* timer drives updates */ }

    private void Refresh()
    {
        if (!_hasLocation) return;
        if (_screenWidth < 2 || _screenHeight < 2) return;

        var utcNow = DateTime.UtcNow;

        var projected = new List<(Star, PointF)>();
        Star? highlighted = null;
        double closestDist = double.MaxValue;

        double cx = _screenWidth / 2.0;
        double cy = _screenHeight / 2.0;

        double focal = ProjectionMath.FocalLengthPx(_screenWidth, _fovHorizontalDeg);
        double highlightPx = focal * Math.Tan(HighlightRadiusDeg * Math.PI / 180.0);

        bool useQuat = _orientation.HasQuaternion;
        double[]? r = useQuat
            ? OrientationMath.RotationMatrix(
                _orientation.QuatX, _orientation.QuatY,
                _orientation.QuatZ, _orientation.QuatW)
            : null;

        double azCal = _orientation.AzimuthCalibrationDeg;
        double altCal = _orientation.AltitudeCalibrationDeg;

        foreach (var star in _allStars)
        {
            double raDeg = star.RA * 15.0;
            AstronomyService.EquatorialToHorizontal(
                raDeg, star.Dec,
                _latitudeDeg, _longitudeDeg,
                utcNow,
                out double az, out double alt);

            if (alt < -5.0) continue;

            var (wE, wN, wU) = ProjectionMath.AzAltToWorldVector(az - azCal, alt - altCal);

            double dx, dy, dz;
            if (useQuat)
            {
                (dx, dy, dz) = OrientationMath.WorldToDevice(r!, wE, wN, wU);
            }
            else
            {
                double devAz = _orientation.AzimuthDeg * Math.PI / 180.0;
                double devAlt = _orientation.AltitudeDeg * Math.PI / 180.0;
                double dEastRel = wE * Math.Cos(devAz) - wN * Math.Sin(devAz);
                double dNorthRel = wE * Math.Sin(devAz) + wN * Math.Cos(devAz);
                dx = dEastRel;
                dy = wU * Math.Cos(devAlt) - dNorthRel * Math.Sin(devAlt);
                dz = -(dNorthRel * Math.Cos(devAlt) + wU * Math.Sin(devAlt));
            }

            var screen = ProjectionMath.Project(dx, dy, dz, cx, cy, focal);
            if (screen is null) continue;
            double sx = screen.Value.sx, sy = screen.Value.sy;

            if (sx < -50 || sx > _screenWidth + 50) continue;
            if (sy < -50 || sy > _screenHeight + 50) continue;

            projected.Add((star, new PointF((float)sx, (float)sy)));

            double pxDist = Math.Sqrt((sx - cx) * (sx - cx) + (sy - cy) * (sy - cy));
            if (pxDist < highlightPx && pxDist < closestDist)
            {
                closestDist = pxDist;
                highlighted = star;
            }
        }

        StarDrawable.Update(projected, highlighted);

        HasHighlightedStar = highlighted is not null;
        HighlightedStarName = highlighted?.Name ?? string.Empty;

        RedrawRequested?.Invoke();
    }

    // ---- Calibration ----

    [RelayCommand]
    private void ToggleLandmarkPicker()
    {
        if (CalibrationTarget is not null) return;
        IsLandmarkPickerVisible = !IsLandmarkPickerVisible;
    }

    [RelayCommand]
    private void SelectLandmark(Landmark landmark)
    {
        IsLandmarkPickerVisible = false;
        CalibrationTarget = landmark;
    }

    [RelayCommand]
    private void ConfirmCalibration()
    {
        if (CalibrationTarget is null || !_hasLocation) return;

        double trueBearing = GeoMath.BearingDeg(
            _latitudeDeg, _longitudeDeg,
            CalibrationTarget.Latitude, CalibrationTarget.Longitude);

        double trueElevation = GeoMath.ElevationAngleDeg(
            _latitudeDeg, _longitudeDeg, observerElevationM: 0,
            CalibrationTarget.Latitude, CalibrationTarget.Longitude,
            CalibrationTarget.ElevationMeters);

        double azOffset = trueBearing - _orientation.AzimuthDeg;
        double altOffset = trueElevation - _orientation.AltitudeDeg;

        _orientation.SetCalibration(azOffset, altOffset);

        CalibrationTarget = null;
        UpdateCalibrationStatusText();
    }

    [RelayCommand]
    private void CancelCalibration()
    {
        CalibrationTarget = null;
        IsLandmarkPickerVisible = false;
    }

    [RelayCommand]
    private void ClearCalibration()
    {
        _orientation.ClearCalibration();
        UpdateCalibrationStatusText();
    }

    private void UpdateCalibrationStatusText()
    {
        double az = _orientation.AzimuthCalibrationDeg;
        double alt = _orientation.AltitudeCalibrationDeg;
        if (Math.Abs(az) < 0.1 && Math.Abs(alt) < 0.1)
        {
            CalibrationStatusText = string.Empty;
        }
        else
        {
            CalibrationStatusText = $"Calibration: az {az:+0.0;-0.0;0}°  alt {alt:+0.0;-0.0;0}°";
        }
    }

    // ---- Location ----

    private async Task RequestLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                LocationText = "No GPS access — using Warsaw";
                _latitudeDeg = 52.229676;
                _longitudeDeg = 21.012229;
                _hasLocation = true;
                return;
            }

            var loc = await Geolocation.Default.GetLastKnownLocationAsync()
                      ?? await Geolocation.Default.GetLocationAsync(
                             new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(10)));

            if (loc is not null)
            {
                _latitudeDeg = loc.Latitude;
                _longitudeDeg = loc.Longitude;
                _hasLocation = true;
                LocationText = $"{loc.Latitude:F2}°N  {loc.Longitude:F2}°E";
            }
            else
            {
                LocationText = "GPS unavailable — using Warsaw";
                _latitudeDeg = 52.229676;
                _longitudeDeg = 21.012229;
                _hasLocation = true;
            }
        }
        catch
        {
            LocationText = "GPS error — using Warsaw";
            _latitudeDeg = 52.229676;
            _longitudeDeg = 21.012229;
            _hasLocation = true;
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
        _orientation.OrientationChanged -= OnOrientationChanged;
        _orientation.Stop();
        _orientation.Dispose();
    }
}
