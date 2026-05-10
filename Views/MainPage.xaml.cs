using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using StarsTracker.ViewModels;

namespace StarsTracker.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private bool _started;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.RedrawRequested += () => StarOverlay.Invalidate();

        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;
        Console.Error.WriteLine("[StarsTracker] Page Loaded");
        await TryStartCamera();
        await _vm.InitializeAsync();
    }

    // Belt-and-suspenders: also try from OnAppearing in case Loaded fires too early
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Console.Error.WriteLine("[StarsTracker] OnAppearing");
        // Give the view 600ms to connect its handler if Loaded already ran
        await Task.Delay(600);
        await TryStartCamera();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraView.StopCameraPreview();
        _vm.Dispose();
    }

    private async Task TryStartCamera()
    {
        if (_started) return;
        Console.Error.WriteLine("[StarsTracker] TryStartCamera");
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            Console.Error.WriteLine($"[StarsTracker] Permission: {status}");
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted) return;

            var cameras = await CameraView.GetAvailableCameras(CancellationToken.None);
            Console.Error.WriteLine($"[StarsTracker] Cameras: {cameras.Count}");
            if (!cameras.Any()) return;

            CameraView.SelectedCamera = cameras
                .FirstOrDefault(c => c.Position == CameraPosition.Rear)
                ?? cameras.First();

            Console.Error.WriteLine($"[StarsTracker] Starting preview...");
            await CameraView.StartCameraPreview(CancellationToken.None);
            _started = true;
            Console.Error.WriteLine("[StarsTracker] Preview started OK");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[StarsTracker] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }

    private void OnOverlaySizeChanged(object? sender, EventArgs e)
    {
        _vm.SetScreenSize(StarOverlay.Width, StarOverlay.Height);
    }
}
