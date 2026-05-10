# StarsTracker

[![Tests](https://github.com/Jakub-Syrek/StarsTracker/actions/workflows/tests.yml/badge.svg)](https://github.com/Jakub-Syrek/StarsTracker/actions/workflows/tests.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform: Android](https://img.shields.io/badge/Platform-Android-green.svg)](https://www.android.com/)

Real-time augmented-reality star identifier for Android. Live camera preview
overlaid with names of the stars currently visible in the frame, computed
from the device's GPS, UTC clock, and orientation sensors. Built with
.NET MAUI 10 and AndroidX CameraX.

## Quick Start

```powershell
# Build
dotnet build StarsTracker.csproj -c Debug -f net10.0-android

# Run unit tests (83 tests over the pure-math Core)
dotnet test StarsTracker.Tests/StarsTracker.Tests.csproj

# Deploy to a connected Android device
dotnet publish StarsTracker.csproj -c Debug -f net10.0-android -p:AndroidPackageFormat=apk
adb install -r bin/Debug/net10.0-android/com.jakubsyrek.starstracker-Signed.apk
```

## Features

- Live rear-camera preview as the page background, integrated through
  AndroidX `PreviewView` injected directly into the activity content view.
- Real-time star overlay rendered with `IDrawable` on top of the camera —
  star dots scaled by magnitude, names labelled for the brightest entries,
  yellow highlight ring for whichever star sits closest to the crosshair.
- Catalogue of ~300 brightest stars (HYG dataset) embedded as `stars.json`.
- Astronomical math from RA/Dec to local Az/Alt using Julian Date and
  Greenwich Mean Sidereal Time.
- Full 3D pinhole projection driven by the rotation-vector quaternion, so
  yaw, pitch and roll are all handled — stars stay locked to the sky as
  the phone tilts in any axis.
- Manual landmark calibration to compensate for magnetometer offset:
  pick a landmark from a Krakow-area catalogue, aim the crosshair at it,
  tap **Confirm** — the offset is computed from the great-circle bearing
  to the landmark and persisted to `Preferences`.
- One-tap **screen recording** through `MediaProjection` and `MediaRecorder`
  (foreground service, `mediaProjectionType`). Output saved to
  `/sdcard/Movies/StarsTracker/` and republished to the Gallery.
- GPS fallback to Warsaw when location permission is denied or unavailable.

## Architecture

The repository is split into three projects so that the math layer can be
unit-tested in isolation from the platform-specific runtime:

| Project | Target | Purpose |
|---------|--------|---------|
| `StarsTracker` | `net10.0-android` | MAUI Android app: views, view models, sensors, camera, screen recording. |
| `StarsTracker.Core` | `net10.0` | Pure-math layer: astronomy, geo math, pinhole projection, quaternion → device-frame transform, landmark catalogue. No platform dependencies. |
| `StarsTracker.Tests` | `net10.0` | xUnit + FluentAssertions test suite (83 tests) covering every public API of `StarsTracker.Core`. |

Design highlights:

- **Single Responsibility** — each service has one role. `OrientationMath`
  is pure quaternion math, `ProjectionMath` is pure pinhole projection,
  `OrientationService` is the platform-side sensor wrapper, `MainViewModel`
  is the runtime orchestration.
- **Dependency Injection** — every service is registered in `MauiProgram`
  (`AddSingleton<StarCatalogService>`, `AddSingleton<OrientationService>`,
  `AddSingleton<LandmarkService>`, plus an explicit factory for
  `MainPage` to make constructor selection unambiguous).
- **No platform leakage in the math** — `StarsTracker.Core` does not
  reference Microsoft.Maui, AndroidX, or Microsoft.Maui.Devices.Sensors,
  so it builds and tests on any .NET 10 host (CI, dev box, Linux runner).
- **Native CameraX integration** — the toolkit camera left a blank screen,
  so `PreviewView` is injected as the first child of the activity's
  `CoordinatorLayout` (z-order 0, beneath the MAUI hierarchy with a
  transparent `ContentPage`). `Implementation Mode = Compatible` uses
  `TextureView` to avoid SurfaceView hardware-overlay compositing.

## Test Coverage

Run with `dotnet test StarsTracker.Tests/StarsTracker.Tests.csproj`.

| Suite | Tests | Covers |
|-------|------:|--------|
| AstronomyServiceTests | 12 | Polaris altitude vs latitude, hemisphere visibility, range invariants, sidereal-day repeatability, meridian transit |
| GeoMathTests | 12 | Krakow ↔ Warsaw distance/bearing, cardinal directions, symmetry, divide-by-zero edge cases, Wawel ↔ Kopiec Kościuszki |
| OrientationMathTests | 10 | Identity quaternion, ±90° around each axis, det(R)=1, length-preservation, range constraints |
| ProjectionMathTests | 15 | Focal-length formula, range validation, az/alt → world vector, perspective projection sanity (centre, off-axis, behind-camera culling, depth scaling) |
| LandmarkServiceTests | 7 | Catalog non-empty, unique names, plausible Krakow-area coordinates |
| PipelineIntegrationTests | 8 | End-to-end (catalog → astronomy → projection): on-axis lands at centre, east star to the right, calibration shift matches expected pixel delta, etc. |
| **Total** | **64+** | All passing |

## Manual Calibration Workflow

Phone magnetometers commonly carry a 10–30° azimuth offset due to local
magnetic interference, which throws the entire star overlay off. The app
ships with a manual calibration flow against a known landmark:

1. Tap **Calibrate** — the bottom drawer reveals a landmark list (Kopiec
   Kościuszki, Wawel, Mariacka, Zakrzówek, Skałki Twardowskiego, etc.).
2. Pick a landmark — the aim overlay appears with instructions.
3. Aim the crosshair at the landmark and tap **Confirm**.
4. The app computes the true bearing from the current GPS to the
   landmark via the great-circle formula, captures the sensor's reported
   azimuth at that moment, and stores the delta as a calibration offset.
5. The offset is applied to every subsequent star projection. **Reset**
   clears it.

## Permissions

Requested at runtime (in `MainActivity.OnCreate`):

- `CAMERA` — for the live preview.
- `ACCESS_FINE_LOCATION` / `ACCESS_COARSE_LOCATION` — for sidereal time
  + bearing calculations against landmarks.
- `POST_NOTIFICATIONS` (Android 13+) — for the recording-status notification.
- `MediaProjection` consent (system dialog) — only when the user starts
  screen recording.

Declared in `AndroidManifest.xml`:

- `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PROJECTION` — required
  by Android 10+ for the recording service.

## Versioning

Semantic versioning (MAJOR.MINOR.PATCH). Version is set in `StarsTracker.csproj`
and bumped automatically on merge to `master` via the `version.yml` workflow:

| Commit type | Bump |
|-------------|------|
| `feat:` | minor (1.0.0 → 1.1.0) |
| `fix:`, `docs:`, `test:`, `refactor:`, `perf:` | patch (1.0.0 → 1.0.1) |
| `BREAKING CHANGE:` in body | major (1.0.0 → 2.0.0) |

## Documentation

- [ABOUT.md](ABOUT.md) — short project summary
- [CHANGELOG.md](CHANGELOG.md) — release history
- [SECURITY.md](SECURITY.md) — vulnerability reporting
- [.github/BRANCH_PROTECTION.md](.github/BRANCH_PROTECTION.md) — branch
  protection setup

## License

MIT

## Support

Open an issue at <https://github.com/Jakub-Syrek/StarsTracker/issues>.
