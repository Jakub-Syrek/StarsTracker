using StarsTracker.Services;

namespace StarsTracker.Tests;

/// <summary>
/// Validates the pinhole-camera math: focal-length derivation, world-vector
/// construction from azimuth/altitude, and the perspective projection.
/// </summary>
public sealed class ProjectionMathTests
{
    [Theory]
    [InlineData(1080, 65)]
    [InlineData(2400, 90)]
    [InlineData(720, 45)]
    public void FocalLength_IsPositive_ForReasonableInputs(double width, double fovDeg)
    {
        double f = ProjectionMath.FocalLengthPx(width, fovDeg);
        f.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FocalLength_FormulaMatches_HalfWidthOverTanHalfFov()
    {
        const double width = 1000;
        const double fovDeg = 60;
        double expected = (width / 2.0) / Math.Tan(fovDeg * Math.PI / 360.0);
        double actual = ProjectionMath.FocalLengthPx(width, fovDeg);
        actual.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void FocalLength_RejectsNonPositiveWidth(double width)
    {
        var act = () => ProjectionMath.FocalLengthPx(width, 60);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(180)]
    [InlineData(-30)]
    public void FocalLength_RejectsInvalidFov(double fov)
    {
        var act = () => ProjectionMath.FocalLengthPx(1000, fov);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AzAltToWorldVector_NorthHorizon_IsUnitNorth()
    {
        var (e, n, u) = ProjectionMath.AzAltToWorldVector(azDeg: 0, altDeg: 0);
        e.Should().BeApproximately(0, 1e-9);
        n.Should().BeApproximately(1, 1e-9);
        u.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void AzAltToWorldVector_EastHorizon_IsUnitEast()
    {
        var (e, n, u) = ProjectionMath.AzAltToWorldVector(azDeg: 90, altDeg: 0);
        e.Should().BeApproximately(1, 1e-9);
        n.Should().BeApproximately(0, 1e-9);
        u.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void AzAltToWorldVector_Zenith_IsUnitUp()
    {
        var (e, n, u) = ProjectionMath.AzAltToWorldVector(azDeg: 137, altDeg: 90);
        e.Should().BeApproximately(0, 1e-9);
        n.Should().BeApproximately(0, 1e-9);
        u.Should().BeApproximately(1, 1e-9);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 30)]
    [InlineData(200, -15)]
    [InlineData(355, 75)]
    public void AzAltToWorldVector_AlwaysUnitLength(double az, double alt)
    {
        var (e, n, u) = ProjectionMath.AzAltToWorldVector(az, alt);
        double len = Math.Sqrt(e * e + n * n + u * u);
        len.Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void Project_PointBehindCamera_ReturnsNull()
    {
        // dz >= 0 means behind camera (camera looks along -Z)
        var p = ProjectionMath.Project(dx: 1, dy: 1, dz: 1, cx: 500, cy: 500, focalPx: 1000);
        p.Should().BeNull();

        var pZero = ProjectionMath.Project(dx: 0, dy: 0, dz: 0, cx: 500, cy: 500, focalPx: 1000);
        pZero.Should().BeNull();
    }

    [Fact]
    public void Project_DirectlyAhead_LandsAtCenter()
    {
        var p = ProjectionMath.Project(dx: 0, dy: 0, dz: -1, cx: 500, cy: 500, focalPx: 1000);
        p.Should().NotBeNull();
        p!.Value.sx.Should().BeApproximately(500, 1e-9);
        p.Value.sy.Should().BeApproximately(500, 1e-9);
    }

    [Fact]
    public void Project_OneUnitToTheRight_ProducesCorrectPixelOffset()
    {
        // Point at (1, 0, -1) projects to cx + focal*1, cy
        var p = ProjectionMath.Project(dx: 1, dy: 0, dz: -1, cx: 500, cy: 500, focalPx: 1000);
        p.Should().NotBeNull();
        p!.Value.sx.Should().BeApproximately(1500, 1e-9);
        p.Value.sy.Should().BeApproximately(500, 1e-9);
    }

    [Fact]
    public void Project_OneUnitUp_ProducesNegativeYOffset()
    {
        // Device Y up should map to screen Y up (smaller screen Y)
        var p = ProjectionMath.Project(dx: 0, dy: 1, dz: -1, cx: 500, cy: 500, focalPx: 1000);
        p.Should().NotBeNull();
        p!.Value.sy.Should().BeApproximately(-500, 1e-9);
    }

    [Fact]
    public void Project_Halving_DistanceDoublesOffset()
    {
        var p1 = ProjectionMath.Project(dx: 1, dy: 0, dz: -2, cx: 0, cy: 0, focalPx: 1000);
        var p2 = ProjectionMath.Project(dx: 1, dy: 0, dz: -1, cx: 0, cy: 0, focalPx: 1000);
        p1!.Value.sx.Should().BeApproximately(p2!.Value.sx / 2, 1e-9);
    }
}
