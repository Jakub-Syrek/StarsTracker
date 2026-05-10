using StarsTracker.ViewModels;

#if ANDROID
using Android.Runtime;
using Android.Views;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Java.Lang;
using Java.Util.Concurrent;
using Microsoft.Maui.ApplicationModel;
#endif

namespace StarsTracker.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private int _cameraStartState; // 0 = idle, 1 = starting/started

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
        Log("Loaded");

        await StartCameraAsync();
        await _vm.InitializeAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Log("OnAppearing");
        await StartCameraAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        System.Threading.Interlocked.Exchange(ref _cameraStartState, 0);
#if ANDROID
        StopNativeCamera();
#endif
        _vm.Dispose();
    }

    private async Task StartCameraAsync()
    {
        // Atomic guard so concurrent OnPageLoaded + OnAppearing don't both start CameraX
        if (System.Threading.Interlocked.CompareExchange(ref _cameraStartState, 1, 0) != 0) return;
        Log("StartCameraAsync");

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                Log("Camera permission denied");
                System.Threading.Interlocked.Exchange(ref _cameraStartState, 0);
                return;
            }

#if ANDROID
            await StartNativeCameraAsync();
#endif
        }
        catch (System.Exception ex)
        {
            Log($"StartCamera error: {ex.GetType().Name}: {ex.Message}");
            System.Threading.Interlocked.Exchange(ref _cameraStartState, 0);
        }
    }

#if ANDROID
    private ProcessCameraProvider? _cameraProvider;
    private PreviewView? _previewView;
    private Android.Views.ViewGroup? _previewParent;

    private async Task StartNativeCameraAsync()
    {
        Log("StartNativeCameraAsync");

        var activity = Platform.CurrentActivity as AndroidX.Activity.ComponentActivity;
        if (activity is null)
        {
            Log("CurrentActivity is null or not ComponentActivity");
            return;
        }

        // Wait briefly for the activity's content view to be ready
        Android.Views.ViewGroup? rootContent = null;
        int waited = 0;
        while (waited < 3000)
        {
            rootContent = activity.FindViewById<Android.Views.ViewGroup>(Android.Resource.Id.Content);
            if (rootContent is { ChildCount: > 0 }) break;
            await Task.Delay(100);
            waited += 100;
        }
        if (rootContent is null || rootContent.ChildCount == 0)
        {
            Log("rootContent is null or has no children");
            return;
        }

        // Inject PreviewView as the FIRST child of MAUI's content root (index 0).
        // MAUI's ContentViewGroup has a custom measure/layout that ignores native
        // children, so PreviewView would render at size 0. The activity content
        // root is a regular FrameLayout that lays out all children at full size,
        // and z-order index 0 puts it BENEATH the MAUI hierarchy.
        var rootChild = rootContent.GetChildAt(0) as Android.Views.ViewGroup ?? rootContent;
        _previewParent = rootChild;

        var previewView = new PreviewView(activity);
        previewView.SetImplementationMode(PreviewView.ImplementationMode.Compatible);
        previewView.LayoutParameters = new Android.Views.ViewGroup.LayoutParams(
            Android.Views.ViewGroup.LayoutParams.MatchParent,
            Android.Views.ViewGroup.LayoutParams.MatchParent);
        _previewView = previewView;

        activity.RunOnUiThread(() =>
        {
            rootChild.AddView(previewView, 0);
            Log($"PreviewView injected at index 0 in {rootChild.GetType().Name} (children={rootChild.ChildCount})");
        });

        Log("Binding CameraX...");

        var tcs = new TaskCompletionSource<bool>();
        var future = ProcessCameraProvider.GetInstance(activity);

        future.AddListener(new Runnable(() =>
        {
            try
            {
                _cameraProvider = (ProcessCameraProvider)future.Get()!;

                var preview = new Preview.Builder().Build();
                preview.SetSurfaceProvider(
                    ContextCompat.GetMainExecutor(activity),
                    previewView.SurfaceProvider);

                _cameraProvider.UnbindAll();
                var camera = _cameraProvider.BindToLifecycle(
                    (ILifecycleOwner)activity,
                    CameraSelector.DefaultBackCamera,
                    preview);

                Log("CameraX bound to lifecycle — preview started");
                TryReportCameraFov(activity);
                tcs.TrySetResult(true);
            }
            catch (System.Exception ex)
            {
                Log($"CameraX bind error: {ex.GetType().Name}: {ex.Message}");
                tcs.TrySetException(ex);
            }
        }), ContextCompat.GetMainExecutor(activity));

        await tcs.Task;
    }

    /// <summary>
    /// Reads the per-device horizontal field of view from Camera2
    /// CameraCharacteristics and forwards it to the view model. Iterates the
    /// physical back-facing cameras and picks the one whose computed FOV
    /// falls in the [55°, 90°] range — the typical "main" lens, distinct
    /// from ultra-wide (>95°) and tele (<50°) sub-cameras present on
    /// flagships. Falls back silently to the hardcoded default on any error.
    /// </summary>
    private void TryReportCameraFov(AndroidX.Activity.ComponentActivity activity)
    {
        try
        {
            var manager = (Android.Hardware.Camera2.CameraManager)
                activity.GetSystemService(Android.Content.Context.CameraService)!;
            var ids = manager.GetCameraIdList();
            if (ids is null || ids.Length == 0) return;

            double bestMain = -1;
            double anyBack = -1;
            string? mainId = null;
            string? anyId = null;

            foreach (var id in ids)
            {
                var chars = manager.GetCameraCharacteristics(id);
                var facingObj = chars.Get(
                    Android.Hardware.Camera2.CameraCharacteristics.LensFacing!);
                int facing = facingObj?.JavaCast<Java.Lang.Integer>()?.IntValue() ?? -1;
                if (facing != (int)Android.Hardware.Camera2.LensFacing.Back) continue;

                var focalsObj = chars.Get(
                    Android.Hardware.Camera2.CameraCharacteristics.LensInfoAvailableFocalLengths!);
                var sensorSizeObj = chars.Get(
                    Android.Hardware.Camera2.CameraCharacteristics.SensorInfoPhysicalSize!);
                float[]? focals = focalsObj?.ToArray<float>();
                var sensorSize = sensorSizeObj?.JavaCast<Android.Util.SizeF>();
                if (focals is null || focals.Length == 0 || sensorSize is null) continue;

                float focalMm = focals.Min();
                float sensorWidthMm = sensorSize.Width;
                double fovDeg = 2.0 * System.Math.Atan(sensorWidthMm / (2.0 * focalMm))
                                    * 180.0 / System.Math.PI;

                if (anyBack < 0) { anyBack = fovDeg; anyId = id; }
                if (fovDeg is >= 55 and <= 90 && bestMain < 0)
                {
                    bestMain = fovDeg;
                    mainId = id;
                }
            }

            double chosen = bestMain > 0 ? bestMain : anyBack;
            string? chosenId = mainId ?? anyId;
            if (chosen < 0)
            {
                Log("FOV detection: no usable back camera characteristics — using default");
                return;
            }

            Log($"Camera2 FOV: {chosen:F1}° (cameraId={chosenId})");
            MainThread.BeginInvokeOnMainThread(() => _vm.SetCameraFov(chosen));
        }
        catch (System.Exception ex)
        {
            Log($"FOV detection failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void StopNativeCamera()
    {
        _cameraProvider?.UnbindAll();
        _cameraProvider = null;

        if (_previewView is not null && _previewParent is not null)
        {
            var pv = _previewView;
            var parent = _previewParent;
            (Platform.CurrentActivity as Android.App.Activity)?.RunOnUiThread(() => parent.RemoveView(pv));
            _previewView = null;
            _previewParent = null;
        }
    }
#endif

    private static void Log(string msg)
    {
        // Appears in logcat as tag "StarsTracker"
#if ANDROID
        Android.Util.Log.Debug("StarsTracker", msg);
#endif
        System.Console.Error.WriteLine($"[StarsTracker] {msg}");
        System.Diagnostics.Debug.WriteLine($"[StarsTracker] {msg}");
    }

    private void OnOverlaySizeChanged(object? sender, EventArgs e)
        => _vm.SetScreenSize(StarOverlay.Width, StarOverlay.Height);
}
