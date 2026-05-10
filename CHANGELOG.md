# Changelog

All notable changes to this project are documented here. The format is based
on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-05-10

### Added

- Live AR star overlay: real-time camera preview with star names projected
  onto the screen, driven by GPS, UTC and the device rotation-vector sensor.
- HYG star catalogue (~300 brightest stars) embedded as `stars.json`.
- Astronomy pipeline (`AstronomyService.EquatorialToHorizontal`) computing
  Az/Alt from RA/Dec, observer location and UTC.
- Full 3D pinhole projection through the rotation-vector quaternion, so
  yaw, pitch and roll are all handled by `R(q)^T · v_world`.
- Manual landmark calibration: pick a Krakow-area landmark (Kopiec
  Kościuszki, Wawel, Zakrzówek, Mariacka, Skałki Twardowskiego, etc.),
  aim the crosshair, tap Confirm — the great-circle bearing offset is
  persisted to `Preferences`.
- Native AndroidX CameraX preview integrated via `PreviewView` injected
  into the activity content view (TextureView mode) for proper composition
  with MAUI overlays.
- Screen recording through `MediaProjection` + `MediaRecorder` in a
  foreground service, output saved to `Movies/StarsTracker/` and published
  to MediaStore for visibility in the Gallery.
- Permissions requested up-front in `MainActivity.OnCreate` (Camera, Fine
  & Coarse Location, POST_NOTIFICATIONS on Android 13+) so CameraX never
  hits a permission wall.
- `StarsTracker.Core` library (`net10.0`) with the pure-math layer,
  separated from the Android app for testability.
- 83-test xUnit suite (`StarsTracker.Tests`) covering Astronomy, GeoMath,
  OrientationMath, ProjectionMath, LandmarkService and an end-to-end
  pipeline integration test.

### Documentation

- Initial `README.md`, `ABOUT.md`, `SECURITY.md`, `CHANGELOG.md`.
- `.github/` workflows, issue templates, pull-request template,
  branch-protection guide.

[Unreleased]: https://github.com/Jakub-Syrek/StarsTracker/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Jakub-Syrek/StarsTracker/releases/tag/v1.0.0
