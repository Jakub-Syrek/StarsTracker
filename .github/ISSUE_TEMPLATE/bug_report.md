---
name: Bug report
about: Report a defect that breaks the AR overlay, camera or recording
title: 'bug: <short description>'
labels: bug
assignees: Jakub-Syrek
---

## Summary

A clear and concise description of what is broken.

## Reproduction steps

1. ...
2. ...
3. ...

## Expected behaviour

What should have happened.

## Actual behaviour

What actually happened. Include screenshots / a screen recording when the
bug is visual.

## Environment

- App version (from "Calibrate" toolbar long-press, or `dotnet list package` output): `1.x.x`
- Device model: `e.g. Samsung Galaxy S25 Ultra`
- Android version: `e.g. 14`
- Sensor support: does **Settings → Developer options** list a rotation
  vector sensor?

## Logs (optional but very helpful)

```
adb logcat -d -s StarsTracker:V
```

Paste the relevant lines.

## Additional context

Anything else worth knowing — magnetic interference nearby, building
materials, recent OS updates, etc.
