# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 0.1.x   | ✅        |

Pre-`0.1.0` builds are unreleased and not maintained — please upgrade.

## Reporting a Vulnerability

**Do not file public GitHub issues for security problems.**

If you discover a vulnerability, send a private email to:

> **jakubvonsyrek@gmail.com**

Please include:

1. A description of the vulnerability and its impact.
2. Reproduction steps (or a minimal proof-of-concept).
3. The affected version (e.g. `1.0.0`) and Android version.
4. Whether you would like to be credited in the release notes.

You should expect an acknowledgement within **3 business days**, and a
status update or fix within **14 days** for confirmed issues.

## Security Posture

StarsTracker runs entirely on-device and has no backend:

- No network calls are made by the app itself. The `INTERNET` permission
  is declared only because Google Play's restore behaviour is happier
  with it; it is never used at runtime.
- Location data (GPS lat/lon) is read from the device, used in-memory for
  bearing and sidereal-time calculations, and is **never** transmitted
  off-device.
- Calibration offsets and other preferences are stored in
  `Preferences.Default` (sandboxed app-private storage).
- Recorded videos go to the standard public `Movies/StarsTracker/` folder
  via `MediaStore`, exactly like Android's built-in screen recorder.
- The MediaProjection consent dialog is shown by the OS each time the
  user taps "Record" — there is no way for the app to record without a
  fresh user grant per session.

## Best Practices for Users

- Grant **only** the permissions the app needs (Camera + Location +
  optional Notifications). The app will fall back to a Warsaw default
  location if you deny GPS.
- Audit the recorded MP4s before sharing — the camera frame may contain
  identifying surroundings.
- Revoke **MediaProjection** consent and stop recording before opening
  apps that show sensitive content (banking, password managers).

## Best Practices for Contributors

- Never commit secrets, API keys, signing certificates, or `.keystore`
  files. The repository contains no production keys; APKs built locally
  are signed with the default debug key only.
- New permissions in `AndroidManifest.xml` must be justified in the PR
  description — we keep the permission surface as small as possible.
- Any new code that handles Personally Identifiable Information (PII)
  must be reviewed against this document before merge.

## Known Vulnerabilities

None at the time of writing.

## Deployment Checklist

Before publishing a release build:

- [ ] `dotnet test StarsTracker.Tests/StarsTracker.Tests.csproj` is green.
- [ ] No new permissions added without justification.
- [ ] `INTERNET` permission still unused at runtime (grep the codebase).
- [ ] Release APK signed with a production keystore (NOT the debug key).
- [ ] CHANGELOG.md updated.
- [ ] Version bumped per the directives in
      [development_directives memory](../.claude/projects/.../development_directives.md).
