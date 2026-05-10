using CommunityToolkit.Maui.Views;
using StarsTracker.ViewModels;

namespace StarsTracker.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        _vm.RedrawRequested += () => StarOverlay.Invalidate();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Request camera permission and start preview
        var camStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (camStatus != PermissionStatus.Granted)
            camStatus = await Permissions.RequestAsync<Permissions.Camera>();

        if (camStatus == PermissionStatus.Granted)
            await CameraView.StartCameraPreview(CancellationToken.None);

        await _vm.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraView.StopCameraPreview();
        _vm.Dispose();
    }

    private void OnOverlaySizeChanged(object? sender, EventArgs e)
    {
        _vm.SetScreenSize(StarOverlay.Width, StarOverlay.Height);
    }
}
