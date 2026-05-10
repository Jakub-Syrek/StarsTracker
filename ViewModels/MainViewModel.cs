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

        double deviceAz = _orientation.AzimuthDeg;
        double deviceAlt = _orientation.AltitudeDeg;
        var utcNow = DateTime.UtcNow;

        var projected = new List<(Star, PointF)>();
        Star? highlighted = null;
        double closestDist = double.MaxValue;

        double fovV = FovH * (_screenHeight / _screenWidth);
        double pxPerDegH = _screenWidth / FovH;
        double pxPerDegV = _screenHeight / fovV;
        double cx = _screenWidth / 2.0;
        double cy = _screenHeight / 2.0;

        foreach (var star in _allStars)
        {
            double raDeg = star.RA * 15.0; // hours → degrees
            AstronomyService.EquatorialToHorizontal(
                raDeg, star.Dec,
                _latitudeDeg, _longitudeDeg,
                utcNow,
                out double az, out double alt);

            // Only show stars above horizon
            if (alt < -5.0) continue;

            // Angular offset from device pointing direction
            double dAz = DeltaAngle(az, deviceAz);
            double dAlt = alt - deviceAlt;

            // Cull stars outside FOV (+30% margin for labels near edge)
            if (Math.Abs(dAz) > FovH * 0.8) continue;
            if (Math.Abs(dAlt) > fovV * 0.8) continue;

            float sx = (float)(cx + dAz * pxPerDegH);
            float sy = (float)(cy - dAlt * pxPerDegV); // screen Y increases downward

            projected.Add((star, new PointF(sx, sy)));

            // Track closest to crosshair for highlighting
            double dist = Math.Sqrt(dAz * dAz + dAlt * dAlt);
            if (dist < HighlightRadiusDeg && dist < closestDist)
            {
                closestDist = dist;
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

    // ---- Helpers ----

    /// <summary>Shortest signed angle from a to b, in range (-180, 180].</summary>
    private static double DeltaAngle(double a, double b)
    {
        double d = a - b;
        while (d > 180) d -= 360;
        while (d < -180) d += 360;
        return d;
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
