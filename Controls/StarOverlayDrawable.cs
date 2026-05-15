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

    // Current screen size (updated on every Draw call)
    private SizeF _screenSize = new(1, 1);

    // Highlighted star (closest to crosshair)
    private Star? _highlighted;

    /// <summary>
    /// A solar-system body projected to screen pixels with the colour and
    /// radius the drawable should render it with.
    /// </summary>
    public readonly record struct ProjectedPlanet(
        string Name, PointF Position, Color Color, float Radius);

    public void Update(
        IReadOnlyList<(Star Star, PointF Position)> projectedStars,
        Star? highlighted,
        IReadOnlyList<ProjectedPlanet>? projectedPlanets = null,
        IReadOnlyList<(PointF From, PointF To)>? constellationLines = null)
    {
        _projectedStars = projectedStars;
        _highlighted = highlighted;
        _projectedPlanets = projectedPlanets ?? [];
        _constellationLines = constellationLines ?? [];
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _screenSize = dirtyRect.Size;
        float cx = dirtyRect.Center.X;
        float cy = dirtyRect.Center.Y;

        canvas.SaveState();

        // Constellation lines first so star dots draw on top.
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

        DrawCrosshair(canvas, cx, cy);

        canvas.RestoreState();
    }

    private void DrawConstellations(ICanvas canvas)
    {
        if (_constellationLines.Count == 0) return;
        // Soft cyan-blue, low alpha so it never overpowers the stars themselves.
        canvas.StrokeColor = Color.FromRgba(140, 200, 255, 90);
        canvas.StrokeSize = 1.2f;
        foreach (var (from, to) in _constellationLines)
        {
            canvas.DrawLine(from.X, from.Y, to.X, to.Y);
        }
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
        // Outer soft halo — 3 concentric circles with decaying alpha.
        canvas.FillColor = planet.Color.WithAlpha(0.10f);
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius + 14);
        canvas.FillColor = planet.Color.WithAlpha(0.22f);
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius + 8);
        canvas.FillColor = planet.Color.WithAlpha(0.45f);
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius + 4);

        // Solid disc.
        canvas.FillColor = planet.Color;
        canvas.FillCircle(planet.Position.X, planet.Position.Y, planet.Radius);

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
