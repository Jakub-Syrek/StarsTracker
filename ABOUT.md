# About StarsTracker

StarsTracker is an Android augmented-reality app that overlays the names of
the stars currently visible in the sky onto a live camera preview. The
device's GPS, UTC clock and rotation-vector sensor feed an astronomical
pipeline that converts each star's catalogue position into screen pixels in
real time.

## What sets it apart

- **Pure-math core unit-tested in isolation.** The astronomy, geo math,
  quaternion-frame transform and pinhole projection live in a separate
  `net10.0` library with an 83-case xUnit suite. The Android UI is a thin
  layer on top.
- **Native CameraX preview** instead of a toolkit camera control, with
  `PreviewView` injected directly into the activity content view so the
  preview composes correctly under the MAUI hierarchy.
- **Manual landmark calibration** against a Krakow-area catalogue (Kopiec
  Kościuszki, Wawel, Zakrzówek, Mariacka, etc.) compensates for typical
  10–30° magnetometer offsets.
- **Built-in screen recording** via `MediaProjection` + `MediaRecorder`
  in a foreground service — captures camera + overlay in a single MP4.

## Stack

- .NET MAUI 10 (Android-only, `net10.0-android`)
- AndroidX CameraX, MediaProjection, MediaRecorder
- CommunityToolkit.Mvvm
- xUnit, FluentAssertions
- HYG star catalogue (~300 brightest stars)

## Author

Jakub Syrek &lt;jakubvonsyrek@gmail.com&gt;
