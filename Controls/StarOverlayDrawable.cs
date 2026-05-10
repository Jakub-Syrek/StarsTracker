using Microsoft.Maui.Graphics;
using StarsTracker.Models;

namespace StarsTracker.Controls;

/// <summary>
/// IDrawable that renders a star field overlay on top of the camera feed.
/// Stars are projected from horizontal coordinates (Az/Alt) to screen pixels
/// based on the device's current pointing direction and estimated camera FOV.
/// </summary>
public sealed class StarOverlayDrawable : IDrawable
{
    // Camera horizontal field of view in degrees (typical smartphone rear camera)
    private const double FovHorizontalDeg = 65.0;

    // Stars + their computed screen positions (updated by ViewModel each frame)
    private IReadOnlyList<(Star Star, PointF Position)> _projectedStars = [];

    // Current screen size (updated on every Draw call)
    private SizeF _screenSize = new(1, 1);

    // Highlighted star (closest to crosshair)
    private Star? _highlighted;

    public void Update(
        IReadOnlyList<(Star Star, PointF Position)> projectedStars,
        Star? highlighted)
    {
        _projectedStars = projectedStars;
        _highlighted = highlighted;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _screenSize = dirtyRect.Size;
        float cx = dirtyRect.MidX;
        float cy = dirtyRect.MidY;

        canvas.SaveState();

        foreach (var (star, pos) in _projectedStars)
        {
            bool isHighlighted = star == _highlighted;
            DrawStar(canvas, star, pos, isHighlighted);
        }

        DrawCrosshair(canvas, cx, cy);

        canvas.RestoreState();
    }

    private static void DrawStar(ICanvas canvas, Star star, PointF pos, bool highlighted)
    {
        // Size inversely proportional to magnitude (brighter = larger dot)
        float radius = StarRadius(star.Magnitude);

        // Star dot — white with slight blue tint
        canvas.FillColor = highlighted
            ? Colors.Yellow
            : Color.FromRgba(200, 220, 255, 230);
        canvas.FillCircle(pos.X, pos.Y, radius);

        // Glow ring for bright stars (magnitude < 2)
        if (star.Magnitude < 2.0f)
        {
            canvas.StrokeColor = Color.FromRgba(200, 220, 255, 60);
            canvas.StrokeSize = 1f;
            canvas.DrawCircle(pos.X, pos.Y, radius + 4);
        }

        // Label — always show for highlighted, show for bright stars otherwise
        if (highlighted || star.Magnitude < 2.5)
        {
            canvas.FontColor = highlighted ? Colors.Yellow : Color.FromRgba(200, 230, 255, 210);
            canvas.FontSize = highlighted ? 15f : 12f;
            canvas.DrawString(
                star.Name,
                pos.X + radius + 6,
                pos.Y - 6,
                HorizontalAlignment.Left);
        }
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
    /// Magnitude scale: -2 → 10px, 5 → 2px.
    /// </summary>
    private static float StarRadius(double magnitude)
    {
        // Linear interpolation: mag -2 → r=10, mag 5 → r=1.5
        double t = (magnitude + 2.0) / 7.0; // 0 at mag -2, 1 at mag 5
        t = Math.Clamp(t, 0.0, 1.0);
        return (float)(10.0 - t * 8.5);
    }
}
