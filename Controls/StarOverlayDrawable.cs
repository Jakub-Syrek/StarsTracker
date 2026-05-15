using Microsoft.Maui.Graphics;
using StarsTracker.Models;

namespace StarsTracker.Controls;

/// <summary>
/// IDrawable that renders a star field overlay on top of the camera feed.
/// Stars are projected from horizontal coordinates (Az/Alt) to screen pixels
/// based on the device's current pointing direction and estimated camera FOV.
///
/// Visual goodies:
/// - Twinkle: bright stars (mag &lt; 3) pulse softly using a per-star phase
///   so the field has life instead of being static dots.
/// - Glow halos: brighter stars get bigger, softer halo rings.
/// - Magnitude-tinted colour: a coarse white-to-pale-blue gradient by
///   brightness — gives the field the cool nebular feel without needing
///   per-star spectral type from HYG.
/// - Planet halos with a chunky soft-glow ring; Saturn renders with rings.
/// </summary>
public sealed class StarOverlayDrawable : IDrawable
{
    // Stars + their computed screen positions (updated by ViewModel each frame)
    private IReadOnlyList<(Star Star, PointF Position)> _projectedStars = [];
    private IReadOnlyList<ProjectedPlanet> _projectedPlanets = [];
    private IReadOnlyList<(PointF From, PointF To)> _constellationLines = [];
    private IReadOnlyList<ProjectedDeepSky> _projectedDeepSky = [];
    private IReadOnlyList<ProjectedMeteorRadiant> _projectedMeteors = [];

    // Current screen size (updated on every Draw call)
    private SizeF _screenSize = new(1, 1);

    // Highlighted star (closest to crosshair)
    private Star? _highlighted;

    // Planetarium mode: paints a dark gradient + procedural starfield on top
    // of the (blurred) camera feed so the overlay becomes the entire scene
    // with just a hint of the real world bleeding through.
    private bool _planetariumMode;

    public readonly record struct ProjectedPlanet(
        string Name, PointF Position, Color Color, float Radius);

    /// <summary>
    /// A Messier / showpiece deep-sky object projected for the overlay.
    /// </summary>
    public readonly record struct ProjectedDeepSky(
        string Id, string Name, string Type,
        PointF Position, float RadiusPx, Color Color);

    /// <summary>
    /// A meteor-shower radiant projected for the overlay. Includes
    /// metadata for the days-until-peak badge.
    /// </summary>
    public readonly record struct ProjectedMeteorRadiant(
        string Code, string Name, int DaysUntilPeak, int ZenithalHourlyRate,
        PointF Position);

    public void Update(
        IReadOnlyList<(Star Star, PointF Position)> projectedStars,
        Star? highlighted,
        IReadOnlyList<ProjectedPlanet>? projectedPlanets = null,
        IReadOnlyList<(PointF From, PointF To)>? constellationLines = null,
        IReadOnlyList<ProjectedDeepSky>? projectedDeepSky = null,
        IReadOnlyList<ProjectedMeteorRadiant>? projectedMeteors = null,
        bool planetariumMode = false)
    {
        _projectedStars = projectedStars;
        _highlighted = highlighted;
        _projectedPlanets = projectedPlanets ?? [];
        _constellationLines = constellationLines ?? [];
        _projectedDeepSky = projectedDeepSky ?? [];
        _projectedMeteors = projectedMeteors ?? [];
        _planetariumMode = planetariumMode;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _screenSize = dirtyRect.Size;
        float cx = dirtyRect.Center.X;
        float cy = dirtyRect.Center.Y;

        canvas.SaveState();

        // Planetarium scene first (if enabled): dark gradient + procedural
        // sparkle stars that hide the camera image but leave a faint reveal.
        if (_planetariumMode)
        {
            DrawPlanetariumBackdrop(canvas, dirtyRect);
        }

        // Render order (bottom → top):
        //   1. Deep-sky diffuse blobs (background)
        //   2. Constellation lines
        //   3. Stars
        //   4. Planets
        //   5. Meteor-shower radiants
        //   6. Crosshair on top
        DrawDeepSky(canvas);
        DrawConstellations(canvas);

        // Time phase for twinkle (cycles every ~6 seconds).
        double timePhase = (Environment.TickCount % 6000) / 6000.0 * 2 * Math.PI;

        foreach (var (star, pos) in _projectedStars)
        {
            bool isHighlighted = star == _highlighted;
            DrawStar(canvas, star, pos, isHighlighted, timePhase);
        }

        // Planets above stars so they're never occluded by faint background dots.
        foreach (var planet in _projectedPlanets)
        {
            DrawPlanet(canvas, planet);
        }

        // Meteor-shower radiants — animated spokes overlay.
        DrawMeteorRadiants(canvas, timePhase);

        DrawCrosshair(canvas, cx, cy);

        canvas.RestoreState();
    }

    private static void DrawPlanetariumBackdrop(ICanvas canvas, RectF rect)
    {
        // Deep midnight-blue gradient (top: darker, bottom: slightly lighter)
        // at alpha 0.88 so ~12% of the real camera image still shows through.
        for (int band = 0; band < 10; band++)
        {
            float t = band / 10f;
            byte r = (byte)(8  + 6  * t);
            byte g = (byte)(10 + 12 * t);
            byte b = (byte)(22 + 28 * t);
            canvas.FillColor = Color.FromRgba(r, g, b, (byte)225);
            canvas.FillRectangle(
                rect.X, rect.Y + rect.Height * t,
                rect.Width, rect.Height * 0.105f);
        }

        // Procedural sparkle starfield — deterministic per-position random so
        // it doesn't shimmer between frames at the cost of locality. ~400
        // tiny dots scattered uniformly with a magnitude-style brightness
        // distribution.
        var rng = new Random(seed: 0x57A2);
        for (int i = 0; i < 400; i++)
        {
            float x = rect.X + (float)rng.NextDouble() * rect.Width;
            float y = rect.Y + (float)rng.NextDouble() * rect.Height;
            double m = rng.NextDouble();
            float radius = m < 0.92 ? 0.6f : 1.4f;
            byte alpha = (byte)(m < 0.92 ? 110 : 220);
            canvas.FillColor = Color.FromRgba((byte)230, (byte)235, (byte)255, alpha);
            canvas.FillCircle(x, y, radius);
        }

        // Subtle horizontal "Milky Way" haze along the lower third — a wide
        // soft gradient stripe to add depth without overpowering the stars.
        float mwY = rect.Y + rect.Height * 0.62f;
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            byte alpha = (byte)(28 * (1 - Math.Abs(t - 0.5) * 2));
            canvas.FillColor = Color.FromRgba((byte)180, (byte)160, (byte)210, alpha);
            canvas.FillRectangle(
                rect.X,
                mwY + (t - 0.5f) * rect.Height * 0.18f,
                rect.Width,
                rect.Height * 0.04f);
        }
    }

    private void DrawDeepSky(ICanvas canvas)
    {
        if (_projectedDeepSky.Count == 0) return;
        foreach (var obj in _projectedDeepSky)
        {
            // Soft diffuse blob — four concentric circles with decaying alpha
            // so the edge fades to nothing instead of a hard outline.
            float r = obj.RadiusPx;
            canvas.FillColor = obj.Color.WithAlpha(0.06f);
            canvas.FillCircle(obj.Position.X, obj.Position.Y, r);
            canvas.FillColor = obj.Color.WithAlpha(0.12f);
            canvas.FillCircle(obj.Position.X, obj.Position.Y, r * 0.75f);
            canvas.FillColor = obj.Color.WithAlpha(0.22f);
            canvas.FillCircle(obj.Position.X, obj.Position.Y, r * 0.5f);
            canvas.FillColor = obj.Color.WithAlpha(0.40f);
            canvas.FillCircle(obj.Position.X, obj.Position.Y, r * 0.25f);

            // Identifier (e.g. "M31") in the centre, common name below.
            canvas.FontSize = 11f;
            canvas.FontColor = Color.FromRgba(0, 0, 0, 160);
            canvas.DrawString(obj.Id, obj.Position.X + 1, obj.Position.Y + 1,
                HorizontalAlignment.Center);
            canvas.FontColor = obj.Color.WithAlpha(0.95f);
            canvas.DrawString(obj.Id, obj.Position.X, obj.Position.Y,
                HorizontalAlignment.Center);

            canvas.FontSize = 9.5f;
            canvas.FontColor = Color.FromRgba(220, 230, 245, 180);
            canvas.DrawString(obj.Name,
                obj.Position.X, obj.Position.Y + 14,
                HorizontalAlignment.Center);
        }
    }

    private void DrawMeteorRadiants(ICanvas canvas, double timePhase)
    {
        if (_projectedMeteors.Count == 0) return;

        foreach (var radiant in _projectedMeteors)
        {
            // Colour: yellow if active (|days| ≤ 2), softer cyan otherwise.
            bool isActiveNow = Math.Abs(radiant.DaysUntilPeak) <= 2;
            Color core = isActiveNow ? Color.FromArgb("#FFC857") : Color.FromArgb("#7DCFFF");

            // Slowly-rotating spokes giving the radiant a meteor-emerging feel.
            float spokeLen = 18f;
            canvas.SaveState();
            canvas.Rotate((float)(timePhase * 12), radiant.Position.X, radiant.Position.Y);
            canvas.StrokeColor = core.WithAlpha(0.65f);
            canvas.StrokeSize = 1.4f;
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3.0;
                float ex = radiant.Position.X + (float)(Math.Cos(a) * spokeLen);
                float ey = radiant.Position.Y + (float)(Math.Sin(a) * spokeLen);
                canvas.DrawLine(radiant.Position.X, radiant.Position.Y, ex, ey);
            }
            canvas.RestoreState();

            // Central glowing dot.
            canvas.FillColor = core.WithAlpha(0.40f);
            canvas.FillCircle(radiant.Position.X, radiant.Position.Y, 7);
            canvas.FillColor = core;
            canvas.FillCircle(radiant.Position.X, radiant.Position.Y, 3.5f);

            // Label "PER  in 5 d" or "GEM  ACTIVE".
            string badge = isActiveNow
                ? "ACTIVE"
                : radiant.DaysUntilPeak >= 0
                    ? $"in {radiant.DaysUntilPeak} d"
                    : $"{-radiant.DaysUntilPeak} d ago";
            canvas.FontSize = 11f;
            canvas.FontColor = Color.FromRgba(0, 0, 0, 200);
            canvas.DrawString($"{radiant.Code}  {badge}",
                radiant.Position.X + 13, radiant.Position.Y - 9,
                HorizontalAlignment.Left);
            canvas.FontColor = core;
            canvas.DrawString($"{radiant.Code}  {badge}",
                radiant.Position.X + 12, radiant.Position.Y - 10,
                HorizontalAlignment.Left);
        }
    }

    private void DrawConstellations(ICanvas canvas)
    {
        if (_constellationLines.Count == 0) return;

        // Two-pass draw so the lines pop over the camera feed:
        //   1. wide soft glow underlay
        //   2. crisp bright cyan core
        canvas.StrokeColor = Color.FromRgba(120, 200, 255, 80);
        canvas.StrokeSize = 4.0f;
        foreach (var (from, to) in _constellationLines)
            canvas.DrawLine(from.X, from.Y, to.X, to.Y);

        canvas.StrokeColor = Color.FromRgba(180, 230, 255, 230);
        canvas.StrokeSize = 1.8f;
        foreach (var (from, to) in _constellationLines)
            canvas.DrawLine(from.X, from.Y, to.X, to.Y);
    }

    private static void DrawStar(ICanvas canvas, Star star, PointF pos, bool highlighted, double timePhase)
    {
        float baseRadius = StarRadius(star.Magnitude);

        // Twinkle: bright stars (mag < 3) modulate ±25% on a per-star phase.
        float scale = 1f;
        if (star.Magnitude < 3.0)
        {
            // Deterministic per-star phase offset (so each twinkles differently).
            double starPhase = (star.Id * 17 % 100) / 100.0 * 2 * Math.PI;
            scale = (float)(1.0 + 0.25 * Math.Sin(timePhase + starPhase));
        }
        float radius = baseRadius * scale;

        // Magnitude-tinted colour: very bright stars stay warm-white, faint
        // dots drift towards pale blue, evoking a slight Rayleigh shift.
        Color body = highlighted ? Colors.Yellow : TintByMagnitude(star.Magnitude);

        // Layered halo for bright stars: outer soft ring + inner crisp dot.
        if (star.Magnitude < 1.5)
        {
            canvas.FillColor = body.WithAlpha(0.18f);
            canvas.FillCircle(pos.X, pos.Y, radius + 6);
            canvas.FillColor = body.WithAlpha(0.40f);
            canvas.FillCircle(pos.X, pos.Y, radius + 3);
        }
        else if (star.Magnitude < 2.5)
        {
            canvas.FillColor = body.WithAlpha(0.30f);
            canvas.FillCircle(pos.X, pos.Y, radius + 3);
        }

        canvas.FillColor = body;
        canvas.FillCircle(pos.X, pos.Y, radius);

        // Diffraction-spike cross for the very brightest stars (mag < 0).
        if (star.Magnitude < 0.5)
        {
            canvas.StrokeColor = body.WithAlpha(0.45f);
            canvas.StrokeSize = 1.0f;
            float spike = radius + 8f;
            canvas.DrawLine(pos.X - spike, pos.Y, pos.X + spike, pos.Y);
            canvas.DrawLine(pos.X, pos.Y - spike, pos.X, pos.Y + spike);
        }

        // Labels: highlighted always, bright stars otherwise.
        if (highlighted || star.Magnitude < 2.5)
        {
            canvas.FontColor = highlighted ? Colors.Yellow : Color.FromRgba(220, 235, 255, 210);
            canvas.FontSize = highlighted ? 15f : 12f;
            canvas.DrawString(
                star.Name,
                pos.X + baseRadius + 6,
                pos.Y - 6,
                HorizontalAlignment.Left);
        }
    }

    private static Color TintByMagnitude(double magnitude)
    {
        // Map magnitude (-2 = warm white, 5 = pale blue) onto a smooth gradient.
        double t = Math.Clamp((magnitude + 2.0) / 7.0, 0, 1);
        byte r = (byte)(255 - 35 * t);   // 255 → 220
        byte g = (byte)(245 - 10 * t);   // 245 → 235
        byte b = (byte)(225 + 30 * t);   // 225 → 255
        return Color.FromRgba(r, g, b, (byte)230);
    }

    private static void DrawPlanet(ICanvas canvas, ProjectedPlanet planet)
    {
        // Outer soft halo — 5 concentric circles with decaying alpha. For the
        // Sun the halo extends much further so it actually looks radiant.
        bool isSun = planet.Name == "Sun";
        float haloMax = isSun ? planet.Radius * 1.4f : 14f;

        canvas.FillColor = planet.Color.WithAlpha(0.06f);
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius + haloMax);
        canvas.FillColor = planet.Color.WithAlpha(0.10f);
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius + haloMax * 0.66f);
        canvas.FillColor = planet.Color.WithAlpha(0.20f);
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius + haloMax * 0.40f);
        canvas.FillColor = planet.Color.WithAlpha(0.38f);
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius + haloMax * 0.20f);
        canvas.FillColor = planet.Color.WithAlpha(0.65f);
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius + 3);

        // Solid disc.
        canvas.FillColor = planet.Color;
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius);

        // Sun + Moon get a subtle limb highlight to feel 3D.
        if (isSun)
        {
            canvas.FillColor = Color.FromRgba(255, 255, 220, 200);
            canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius * 0.55f);
        }
        else if (planet.Name == "Moon")
        {
            // Slightly off-centre highlight so it looks lit from one side.
            canvas.FillColor = Color.FromRgba(255, 255, 255, 160);
            canvas.FillCircle(
                planet.Position.X - planet.Radius * 0.25f,
                planet.Position.Y - planet.Radius * 0.25f,
                planet.Radius * 0.55f);
        }

        // Saturn gets its rings.
        if (planet.Name == "Saturn")
        {
            DrawSaturnRings(canvas, planet);
        }

        // Label with subtle drop shadow for readability over the camera feed.
        canvas.FontSize = 13f;
        canvas.FontColor = Color.FromRgba(0, 0, 0, 160);
        canvas.DrawString(
            planet.Name,
            planet.Position.X + planet.Radius + 7,
            planet.Position.Y - 5,
            HorizontalAlignment.Left);
        canvas.FontColor = planet.Color.WithAlpha(0.95f);
        canvas.DrawString(
            planet.Name,
            planet.Position.X + planet.Radius + 6,
            planet.Position.Y - 6,
            HorizontalAlignment.Left);
    }

    private static void DrawSaturnRings(ICanvas canvas, ProjectedPlanet planet)
    {
        // Tilted ellipse around Saturn — purely cosmetic, fixed tilt.
        canvas.SaveState();
        canvas.Rotate(-22f, planet.Position.X, planet.Position.Y);

        float ringRx = planet.Radius * 2.4f;
        float ringRy = planet.Radius * 0.7f;

        canvas.StrokeColor = planet.Color.WithAlpha(0.85f);
        canvas.StrokeSize = 1.8f;
        canvas.DrawEllipse(
            planet.Position.X - ringRx, planet.Position.Y - ringRy,
            ringRx * 2, ringRy * 2);

        // Inner faint ring for layered look.
        canvas.StrokeColor = planet.Color.WithAlpha(0.45f);
        canvas.StrokeSize = 1.0f;
        float innerRx = planet.Radius * 1.7f;
        float innerRy = planet.Radius * 0.5f;
        canvas.DrawEllipse(
            planet.Position.X - innerRx, planet.Position.Y - innerRy,
            innerRx * 2, innerRy * 2);

        canvas.RestoreState();
    }

    private static void DrawCrosshair(ICanvas canvas, float cx, float cy)
    {
        const float len = 20f;
        const float gap = 6f;

        canvas.StrokeColor = Color.FromRgba(255, 255, 255, 160);
        canvas.StrokeSize = 1.5f;

        // Horizontal lines
        canvas.DrawLine(cx - len - gap, cy, cx - gap, cy);
        canvas.DrawLine(cx + gap, cy, cx + len + gap, cy);

        // Vertical lines
        canvas.DrawLine(cx, cy - len - gap, cx, cy - gap);
        canvas.DrawLine(cx, cy + gap, cx, cy + len + gap);

        // Centre dot
        canvas.FillColor = Color.FromRgba(255, 255, 255, 180);
        canvas.FillCircle(cx, cy, 2);
    }

    /// <summary>
    /// Maps visual magnitude to dot radius (brighter → bigger dot).
    /// Magnitude scale: -2 → 11px, 5 → 1.5px.
    /// </summary>
    private static float StarRadius(double magnitude)
    {
        double t = (magnitude + 2.0) / 7.0;
        t = Math.Clamp(t, 0.0, 1.0);
        return (float)(11.0 - t * 9.5);
    }
}
