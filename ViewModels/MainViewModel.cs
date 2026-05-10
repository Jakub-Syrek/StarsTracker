using CommunityToolkit.Mvvm.ComponentModel;
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

    // Camera horizontal FOV in degrees (typical smartphone)
    private const double FovH = 65.0;

    // Crosshair radius for highlighting (degrees)
    private const double HighlightRadiusDeg = 5.0;

    private IReadOnlyList<Star> _allStars = [];
    private IDispatcherTimer? _refreshTimer;
    private double _screenWidth = 1;
    private double _screenHeight = 1;

    // ---- Observable properties ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private string _statusText = "Inicjalizacja...";

    public bool IsLoading => !string.IsNullOrEmpty(StatusText);

    [ObservableProperty]
    private string _highlightedStarName = string.Empty;

    [ObservableProperty]
    private bool _hasHighlightedStar;

    [ObservableProperty]
    private string _locationText = string.Empty;

    // The drawable that GraphicsView renders
    public StarOverlayDrawable StarDrawable { get; } = new();

    // Invoked whenever the overlay needs a redraw
    public event Action? RedrawRequested;

    // ---- Location ----
    private double _latitudeDeg;
    private double _longitudeDeg;
    private bool _hasLocation;

    public MainViewModel(StarCatalogService catalog, OrientationService orientation)
    {
        _catalog = catalog;
        _orientation = orientation;
        _orientation.OrientationChanged += OnOrientationChanged;
    }

    public async Task InitializeAsync()
    {
        StatusText = "Ładowanie katalogu gwiazd...";
        _allStars = await _catalog.GetVisibleStarsAsync();

        StatusText = "Pobieranie lokalizacji GPS...";
        await RequestLocationAsync();

        StatusText = "Uruchamianie sensorów...";
        _orientation.Start();

        // Refresh timer: 10 fps is smooth enough for a star tracker
        _refreshTimer = Application.Current!.Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(100);
        _refreshTimer.Tick += (_, _) => Refresh();
        _refreshTimer.Start();

        StatusText = string.Empty; // clears loading indicator
    }

    public void SetScreenSize(double width, double height)
    {
        _screenWidth = width;
        _screenHeight = height;
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

        // Pinhole camera focal length derived from horizontal FOV.
        // Same focal length on both axes (square pixels).
        double focal = (_screenWidth / 2.0) / Math.Tan(FovH * Math.PI / 360.0);

        // Pre-compute rotation matrix R(q) — rotates world (E,N,Up) to device (right,top,out-of-screen).
        bool useQuat = _orientation.HasQuaternion;
        double m00 = 1, m01 = 0, m02 = 0;
        double m10 = 0, m11 = 1, m12 = 0;
        double m20 = 0, m21 = 0, m22 = 1;
        if (useQuat)
        {
            double qx = _orientation.QuatX, qy = _orientation.QuatY,
                   qz = _orientation.QuatZ, qw = _orientation.QuatW;
            m00 = 1 - 2 * (qy * qy + qz * qz);
            m01 = 2 * (qx * qy - qz * qw);
            m02 = 2 * (qx * qz + qy * qw);
            m10 = 2 * (qx * qy + qz * qw);
            m11 = 1 - 2 * (qx * qx + qz * qz);
            m12 = 2 * (qy * qz - qx * qw);
            m20 = 2 * (qx * qz - qy * qw);
            m21 = 2 * (qy * qz + qx * qw);
            m22 = 1 - 2 * (qx * qx + qy * qy);
        }

        // Highlight radius in pixels: HighlightRadiusDeg → angle on screen
        double highlightPx = focal * Math.Tan(HighlightRadiusDeg * Math.PI / 180.0);

        foreach (var star in _allStars)
        {
            double raDeg = star.RA * 15.0;
            AstronomyService.EquatorialToHorizontal(
                raDeg, star.Dec,
                _latitudeDeg, _longitudeDeg,
                utcNow,
                out double az, out double alt);

            if (alt < -5.0) continue;

            // World-frame unit vector for the star (East, North, Up)
            double azRad = az * Math.PI / 180.0;
            double altRad = alt * Math.PI / 180.0;
            double cosAlt = Math.Cos(altRad);
            double wE = cosAlt * Math.Sin(azRad);
            double wN = cosAlt * Math.Cos(azRad);
            double wU = Math.Sin(altRad);

            double dx, dy, dz;
            if (useQuat)
            {
                // Rotate world → device frame
                dx = m00 * wE + m01 * wN + m02 * wU;
                dy = m10 * wE + m11 * wN + m12 * wU;
                dz = m20 * wE + m21 * wN + m22 * wU;
            }
            else
            {
                // Fallback: ignore roll. Synthesize device-frame vector from azimuth/altitude
                // relative to device pointing direction (compass + accelerometer).
                double devAz = _orientation.AzimuthDeg * Math.PI / 180.0;
                double devAlt = _orientation.AltitudeDeg * Math.PI / 180.0;
                double dEastRel  = wE * Math.Cos(devAz) - wN * Math.Sin(devAz);
                double dNorthRel = wE * Math.Sin(devAz) + wN * Math.Cos(devAz);
                dx = dEastRel;
                dy = wU * Math.Cos(devAlt) - dNorthRel * Math.Sin(devAlt);
                dz = -(dNorthRel * Math.Cos(devAlt) + wU * Math.Sin(devAlt));
            }

            // Camera looks along -Z in device frame; cull stars behind us
            if (dz >= 0) continue;

            // Pinhole projection. Screen Y grows downward; device Y grows upward.
            double sx = cx + focal * (dx / -dz);
            double sy = cy - focal * (dy / -dz);

            // Cull off-screen with small margin
            if (sx < -50 || sx > _screenWidth + 50) continue;
            if (sy < -50 || sy > _screenHeight + 50) continue;

            projected.Add((star, new PointF((float)sx, (float)sy)));

            // Highlight star nearest to screen center
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
                LocationText = "Brak dostępu do GPS — używam Warszawy";
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
                LocationText = "Brak GPS — używam Warszawy";
                _latitudeDeg = 52.229676;
                _longitudeDeg = 21.012229;
                _hasLocation = true;
            }
        }
        catch
        {
            LocationText = "Błąd GPS — używam Warszawy";
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
