using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using StarsTracker.Controls;
using StarsTracker.Models;
using StarsTracker.Services;
using StarsTracker.Shared.Contracts;

namespace StarsTracker.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly StarCatalogService _catalog;
    private readonly OrientationService _orientation;
    private readonly LandmarkService _landmarks;
    private readonly SkyServerClient _skyServer;

    // Server-side payloads (refreshed periodically; survive offline via disk cache).
    private IReadOnlyList<PlanetPositionDto> _planets = [];
    private IReadOnlyList<ConstellationDto> _constellations = [];
    private IReadOnlyList<DeepSkyObjectDto> _deepSky = [];
    private IReadOnlyList<MeteorShowerDto> _meteors = [];
    private Dictionary<string, Star> _starsByName = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastPlanetsFetch = DateTime.MinValue;
    private DateTime _lastMeteorsFetch = DateTime.MinValue;

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
    [NotifyPropertyChangedFor(nameof(HasHighlightedStarDistance))]
    private string _highlightedStarDistance = string.Empty;

    public bool HasHighlightedStarDistance => !string.IsNullOrEmpty(HighlightedStarDistance);

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

    public MainViewModel(
        StarCatalogService catalog,
        OrientationService orientation,
        LandmarkService landmarks,
        SkyServerClient skyServer)
    {
        _catalog = catalog;
        _orientation = orientation;
        _landmarks = landmarks;
        _skyServer = skyServer;
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
        // Some HYG entries share a proper name (e.g. components of a double).
        // Keep the brightest entry per name — that's the one a viewer expects
        // to see and points at when scanning the sky.
        _starsByName = _allStars
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Magnitude).First(),
                StringComparer.OrdinalIgnoreCase);

        StatusText = "Acquiring GPS location...";
        await RequestLocationAsync();

        StatusText = "Starting sensors...";
        _orientation.Start();

        // Pull static catalogues once (constellations, deep-sky, full HYG)
        // and the time-dependent ones (planets, meteor showers) immediately.
        // Fire-and-forget — the overlay degrades gracefully without these.
        _ = FetchConstellationsAsync();
        _ = FetchDeepSkyAsync();
        _ = FetchPlanetsAsync(force: true);
        _ = FetchMeteorShowersAsync(force: true);
        _ = FetchExtendedStarsAsync();

        _refreshTimer = Application.Current!.Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(100);
        _refreshTimer.Tick += (_, _) => Refresh();
        _refreshTimer.Start();

        StatusText = string.Empty;
    }

    private async Task FetchConstellationsAsync()
    {
        try
        {
            var result = await _skyServer.GetConstellationsAsync();
            if (result is not null) _constellations = result;
        }
        catch
        {
            // SkyServerClient handles its own offline fallback; swallow here.
        }
    }

    private async Task FetchPlanetsAsync(bool force = false)
    {
        if (!force && (DateTime.UtcNow - _lastPlanetsFetch) < TimeSpan.FromMinutes(10))
            return;

        try
        {
            var result = await _skyServer.GetPlanetsAsync(DateTime.UtcNow);
            if (result is not null)
            {
                _planets = result;
                _lastPlanetsFetch = DateTime.UtcNow;
            }
        }
        catch
        {
        }
    }

    private async Task FetchDeepSkyAsync()
    {
        try
        {
            var result = await _skyServer.GetDeepSkyAsync();
            if (result is not null) _deepSky = result;
        }
        catch
        {
        }
    }

    private async Task FetchExtendedStarsAsync()
    {
        try
        {
            var result = await _skyServer.GetExtendedStarsAsync();
            if (result is null || result.Count == 0) return;

            // Replace the bundled 300-star catalogue with the full HYG set
            // (~5000 entries mag ≤ 6). Rebuild the name lookup so the
            // constellation segments still resolve.
            var merged = result
                .Select(r => new Star(r.Id, r.Name, r.Ra, r.Dec, r.Mag, r.DistLy))
                .OrderBy(s => s.Magnitude)
                .ToList()
                .AsReadOnly();
            _allStars = merged;
            _starsByName = merged
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Magnitude).First(),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
        }
    }

    private async Task FetchMeteorShowersAsync(bool force = false)
    {
        if (!force && (DateTime.UtcNow - _lastMeteorsFetch) < TimeSpan.FromHours(6))
            return;

        try
        {
            var result = await _skyServer.GetMeteorShowersAsync(DateTime.UtcNow);
            if (result is not null)
            {
                _meteors = result;
                _lastMeteorsFetch = DateTime.UtcNow;
            }
        }
        catch
        {
        }
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

    /// <summary>
    /// Projects every body returned by the API onto screen pixels using the
    /// same astronomy + 3D-rotation pipeline as the stars, plus a per-body
    /// colour and radius for the drawable to render.
    /// </summary>
    private List<StarOverlayDrawable.ProjectedPlanet> ProjectPlanets(
        DateTime utcNow,
        double focal,
        double cx,
        double cy,
        double[]? r,
        bool useQuat,
        double azCal,
        double altCal)
    {
        var list = new List<StarOverlayDrawable.ProjectedPlanet>(_planets.Count);
        foreach (var body in _planets)
        {
            AstronomyService.EquatorialToHorizontal(
                body.RightAscensionDeg, body.DeclinationDeg,
                _latitudeDeg, _longitudeDeg,
                utcNow,
                out double az, out double alt);

            alt += AstronomyService.AtmosphericRefractionDeg(alt);
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
            float sx = (float)screen.Value.sx;
            float sy = (float)screen.Value.sy;
            if (sx < -100 || sx > _screenWidth + 100) continue;
            if (sy < -100 || sy > _screenHeight + 100) continue;

            var (color, radius) = PlanetStyle(body.Name);
            list.Add(new StarOverlayDrawable.ProjectedPlanet(
                body.Name, new PointF(sx, sy), color, radius));
        }
        return list;
    }

    /// <summary>
    /// Connects each constellation line's endpoint stars by their projected
    /// pixel positions. Drops the segment when either endpoint is not in
    /// the currently visible star set.
    /// </summary>
    private List<(PointF From, PointF To)> ProjectConstellationLines(
        IReadOnlyDictionary<string, PointF> projectedStarPositions)
    {
        var lines = new List<(PointF, PointF)>();
        foreach (var c in _constellations)
        {
            foreach (var seg in c.Lines)
            {
                if (projectedStarPositions.TryGetValue(seg.FromStar, out var from) &&
                    projectedStarPositions.TryGetValue(seg.ToStar, out var to))
                {
                    lines.Add((from, to));
                }
            }
        }
        return lines;
    }

    /// <summary>
    /// Per-body rendering style. Radii are intentionally exaggerated — the
    /// real angular diameter of the Sun and Moon is only ~0.5°, which on a
    /// 75°-FOV phone screen amounts to ~7 px. We blow that up so the bodies
    /// are immediately recognisable in the AR overlay rather than getting
    /// lost among the background stars.
    /// </summary>
    /// <summary>
    /// Projects each Messier / showpiece deep-sky object into the camera
    /// frame. The size on screen is computed from <c>ApparentSizeArcmin</c>
    /// so M31 looks like the huge oval it really is.
    /// </summary>
    private List<StarOverlayDrawable.ProjectedDeepSky> ProjectDeepSky(
        DateTime utcNow,
        double focal,
        double cx,
        double cy,
        double[]? r,
        bool useQuat,
        double azCal,
        double altCal)
    {
        var list = new List<StarOverlayDrawable.ProjectedDeepSky>(_deepSky.Count);
        foreach (var obj in _deepSky)
        {
            var (raDeg, decDeg) = AstronomyService.PrecessFromJ2000(
                obj.RightAscensionDeg, obj.DeclinationDeg, utcNow);
            AstronomyService.EquatorialToHorizontal(
                raDeg, decDeg, _latitudeDeg, _longitudeDeg, utcNow,
                out double az, out double alt);
            alt += AstronomyService.AtmosphericRefractionDeg(alt);
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
            float sx = (float)screen.Value.sx;
            float sy = (float)screen.Value.sy;
            if (sx < -200 || sx > _screenWidth + 200) continue;
            if (sy < -200 || sy > _screenHeight + 200) continue;

            // Angular radius in degrees → pixel radius via pinhole focal.
            double angularRadiusDeg = obj.ApparentSizeArcmin / 60.0 / 2.0;
            float radiusPx = (float)(focal * Math.Tan(angularRadiusDeg * Math.PI / 180.0));
            // Clamp so tiny pinpoints stay visible (>= 6 px) and huge clouds
            // don't swamp the entire screen.
            radiusPx = Math.Clamp(radiusPx, 6f, (float)(_screenWidth * 0.4));

            list.Add(new StarOverlayDrawable.ProjectedDeepSky(
                obj.Id, obj.Name, obj.Type,
                new PointF(sx, sy), radiusPx, ColorForType(obj.Type)));
        }
        return list;
    }

    /// <summary>
    /// Projects each active meteor-shower radiant into the camera frame so
    /// the drawable can render the spinning "spokes" icon plus the days-
    /// until-peak badge.
    /// </summary>
    private List<StarOverlayDrawable.ProjectedMeteorRadiant> ProjectMeteorRadiants(
        DateTime utcNow,
        double focal,
        double cx,
        double cy,
        double[]? r,
        bool useQuat,
        double azCal,
        double altCal)
    {
        var list = new List<StarOverlayDrawable.ProjectedMeteorRadiant>(_meteors.Count);
        foreach (var shower in _meteors)
        {
            var (raDeg, decDeg) = AstronomyService.PrecessFromJ2000(
                shower.RadiantRightAscensionDeg, shower.RadiantDeclinationDeg, utcNow);
            AstronomyService.EquatorialToHorizontal(
                raDeg, decDeg, _latitudeDeg, _longitudeDeg, utcNow,
                out double az, out double alt);
            alt += AstronomyService.AtmosphericRefractionDeg(alt);
            if (alt < -10.0) continue;

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
            float sx = (float)screen.Value.sx;
            float sy = (float)screen.Value.sy;
            if (sx < -200 || sx > _screenWidth + 200) continue;
            if (sy < -200 || sy > _screenHeight + 200) continue;

            list.Add(new StarOverlayDrawable.ProjectedMeteorRadiant(
                shower.Code, shower.Name, shower.DaysUntilPeak,
                shower.ZenithalHourlyRate,
                new PointF(sx, sy)));
        }
        return list;
    }

    private static Color ColorForType(string type) => type switch
    {
        "Galaxy"            => Color.FromArgb("#9BB8FF"), // cool blue-violet
        "Nebula"            => Color.FromArgb("#FF9E80"), // warm pink-orange (H-alpha)
        "Planetary Nebula"  => Color.FromArgb("#7DE6C9"), // teal-green
        "Open Cluster"      => Color.FromArgb("#FFE082"), // soft yellow
        "Globular Cluster"  => Color.FromArgb("#FFD6A0"), // amber
        "Supernova Remnant" => Color.FromArgb("#E0A0FF"), // lilac
        _                   => Color.FromArgb("#CFCFEF"),
    };

    private static (Color color, float radius) PlanetStyle(string name) => name switch
    {
        "Sun"     => (Color.FromArgb("#FFD24A"), 38f),
        "Moon"    => (Color.FromArgb("#EDEDF5"), 32f),
        "Mercury" => (Color.FromArgb("#C8B98C"), 12f),
        "Venus"   => (Color.FromArgb("#F4E8C8"), 18f),
        "Mars"    => (Color.FromArgb("#E07050"), 16f),
        "Jupiter" => (Color.FromArgb("#E5C284"), 22f),
        "Saturn"  => (Color.FromArgb("#E2D294"), 20f),
        _         => (Colors.White,              10f),
    };

    /// <summary>
    /// Renders a light-year distance with appropriate precision: two
    /// significant figures under 100 ly, integers otherwise.
    /// </summary>
    private static string FormatDistance(double lightYears)
    {
        if (lightYears < 100) return $"{lightYears:0.#} ly away";
        if (lightYears < 1000) return $"{lightYears:0} ly away";
        return $"{lightYears:N0} ly away";
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
            // Catalog is at the J2000 epoch; correct to the observation date
            // (linear approximation, accurate to a few arcseconds for ~50 years).
            double raDegJ2000 = star.RA * 15.0;
            var (raDeg, decDeg) = AstronomyService.PrecessFromJ2000(
                raDegJ2000, star.Dec, utcNow);

            AstronomyService.EquatorialToHorizontal(
                raDeg, decDeg,
                _latitudeDeg, _longitudeDeg,
                utcNow,
                out double az, out double alt);

            // Atmospheric refraction lifts apparent altitude — significant near
            // the horizon (~0.5°), negligible above 30°.
            alt += AstronomyService.AtmosphericRefractionDeg(alt);

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

        // ---- Planets ----
        var projectedPlanets = ProjectPlanets(utcNow, focal, cx, cy, r, useQuat, azCal, altCal);

        // ---- Deep-sky objects ----
        var projectedDeepSky = ProjectDeepSky(utcNow, focal, cx, cy, r, useQuat, azCal, altCal);

        // ---- Meteor radiants ----
        var projectedMeteors = ProjectMeteorRadiants(utcNow, focal, cx, cy, r, useQuat, azCal, altCal);

        // ---- Constellation lines ----
        var projectedStarPositions = projected
            .GroupBy(p => p.Item1.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Item2, StringComparer.OrdinalIgnoreCase);
        var constellationLines = ProjectConstellationLines(projectedStarPositions);

        StarDrawable.Update(
            projected, highlighted,
            projectedPlanets, constellationLines,
            projectedDeepSky, projectedMeteors);

        HasHighlightedStar = highlighted is not null;
        HighlightedStarName = highlighted?.Name ?? string.Empty;
        HighlightedStarDistance = highlighted?.DistanceLightYears is { } d
            ? FormatDistance(d)
            : string.Empty;

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
