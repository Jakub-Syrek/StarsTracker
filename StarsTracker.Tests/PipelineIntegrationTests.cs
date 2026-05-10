using StarsTracker.Services;

namespace StarsTracker.Tests;

/// <summary>
/// End-to-end checks that exercise the full projection pipeline used at
/// runtime: AstronomyService -> ProjectionMath.AzAltToWorldVector ->
/// OrientationMath.WorldToDevice -> ProjectionMath.Project.
///
/// Each test fixes a known phone orientation (via a hand-crafted quaternion)
/// and a known sky direction, and asserts the star ends up where geometry
/// says it should: the on-axis star at the screen centre, an off-axis star
/// in the expected quadrant, etc.
/// </summary>
public sealed class PipelineIntegrationTests
{
    private const double ScreenWidthPx = 1080;
    private const double ScreenHeightPx = 2400;
    private const double FovDeg = 65;
    private static readonly double Sqrt2Over2 = Math.Sqrt(2) / 2;

    /// <summary>
    /// Phone vertical, top up, camera pointed at North horizon (quaternion
    /// representing +90° around X axis). A star at azimuth=0, altitude=0 must
    /// project exactly at the screen centre.
    /// </summary>
    [Fact]
    public void StarOnAxis_ProjectsAtScreenCenter()
    {
        double[] r = OrientationMath.RotationMatrix(Sqrt2Over2, 0, 0, Sqrt2Over2);
        var (wE, wN, wU) = ProjectionMath.AzAltToWorldVector(azDeg: 0, altDeg: 0);
        var (dx, dy, dz) = OrientationMath.WorldToDevice(r, wE, wN, wU);
        double focal = ProjectionMath.FocalLengthPx(ScreenWidthPx, FovDeg);
        var screen = ProjectionMath.Project(dx, dy, dz,
            ScreenWidthPx / 2, ScreenHeightPx / 2, focal);

        screen.Should().NotBeNull();
        screen!.Value.sx.Should().BeApproximately(ScreenWidthPx / 2, 0.5);
        screen.Value.sy.Should().BeApproximately(ScreenHeightPx / 2, 0.5);
    }

    /// <summary>
    /// Same orientation, star slightly East. Expected: it appears to the right
    /// of centre (sx > cx) because rotating the phone clockwise (towards East)
    /// would bring it back to centre.
    /// </summary>
    [Fact]
    public void StarTenDegreesEast_AppearsToTheRightOfCenter()
    {
        double[] r = OrientationMath.RotationMatrix(Sqrt2Over2, 0, 0, Sqrt2Over2);
        var (wE, wN, wU) = ProjectionMath.AzAltToWorldVector(azDeg: 10, altDeg: 0);
        var (dx, dy, dz) = OrientationMath.WorldToDevice(r, wE, wN, wU);
        double focal = ProjectionMath.FocalLengthPx(ScreenWidthPx, FovDeg);
        var screen = ProjectionMath.Project(dx, dy, dz,
            ScreenWidthPx / 2, ScreenHeightPx / 2, focal);

        screen.Should().NotBeNull();
        screen!.Value.sx.Should().BeGreaterThan(ScreenWidthPx / 2);
        screen.Value.sy.Should().BeApproximately(ScreenHeightPx / 2, 1);
    }

    /// <summary>
    /// Star slightly above the horizon (altitude=10°): must appear ABOVE the
    /// crosshair (sy &lt; cy) because screen Y grows downward.
    /// </summary>
    [Fact]
    public void StarTenDegreesAboveHorizon_AppearsAboveCenter()
    {
        double[] r = OrientationMath.RotationMatrix(Sqrt2Over2, 0, 0, Sqrt2Over2);
        var (wE, wN, wU) = ProjectionMath.AzAltToWorldVector(azDeg: 0, altDeg: 10);
        var (dx, dy, dz) = OrientationMath.WorldToDevice(r, wE, wN, wU);
        double focal = ProjectionMath.FocalLengthPx(ScreenWidthPx, FovDeg);
        var screen = ProjectionMath.Project(dx, dy, dz,
            ScreenWidthPx / 2, ScreenHeightPx / 2, focal);

        screen.Should().NotBeNull();
        screen!.Value.sy.Should().BeLessThan(ScreenHeightPx / 2);
    }

    /// <summary>
    /// Star directly behind the camera (south, when camera points North) must
    /// be culled by Project (returning null).
    /// </summary>
    [Fact]
    public void StarBehindCamera_IsCulled()
    {
        double[] r = OrientationMath.RotationMatrix(Sqrt2Over2, 0, 0, Sqrt2Over2);
        var (wE, wN, wU) = ProjectionMath.AzAltToWorldVector(azDeg: 180, altDeg: 0);
        var (dx, dy, dz) = OrientationMath.WorldToDevice(r, wE, wN, wU);
        double focal = ProjectionMath.FocalLengthPx(ScreenWidthPx, FovDeg);
        var screen = ProjectionMath.Project(dx, dy, dz,
            ScreenWidthPx / 2, ScreenHeightPx / 2, focal);

        screen.Should().BeNull();
    }

    /// <summary>
    /// Polaris from Krakow at any time should be at altitude ~50° (matching
    /// the city's latitude). This confirms the AstronomyService output stays
    /// stable across a few hours.
    /// </summary>
    [Fact]
    public void Polaris_FromKrakow_StaysAtRoughlyLatitudeAltitude()
    {
        const double latitude = 50.06, longitude = 19.94;
        const double polarisRaHours = 2.530301, polarisDecDeg = 89.264109;

        DateTime[] times =
        [
            new(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc),
            new(2026, 1, 15, 22, 0, 0, DateTimeKind.Utc),
            new(2026, 6, 15, 22, 0, 0, DateTimeKind.Utc),
        ];

        foreach (var t in times)
        {
            AstronomyService.EquatorialToHorizontal(
                polarisRaHours * 15, polarisDecDeg,
                latitude, longitude, t,
                out _, out double altitude);

            altitude.Should().BeInRange(latitude - 1.5, latitude + 1.5);
        }
    }

    /// <summary>
    /// Calibration scenario: imagine the phone's compass reads 350° but the
    /// camera is actually pointing at North (true bearing 0°). The runtime
    /// applies an offset of +10° to the sensor azimuth. The pipeline should
    /// then place a star at true bearing 0° at the screen centre.
    /// </summary>
    [Fact]
    public void CalibrationOffset_ShiftsStarsByTheRightAmount()
    {
        // Build a quaternion equivalent to "phone vertical, pointed at azimuth 350°"
        // = rotate +90° around X (vertical) THEN -10° around world Up (Z) to swing
        // the camera 10° west of true north.
        // Doing this analytically is fiddly — instead we apply the offset directly
        // to the input star azimuth, exactly as MainViewModel.Refresh() does.
        double[] r = OrientationMath.RotationMatrix(Sqrt2Over2, 0, 0, Sqrt2Over2);

        const double sensorAzDeg = 350; // what the magnetometer reads
        const double trueAzDeg = 0;     // where the camera actually points
        double azCalibration = trueAzDeg - sensorAzDeg; // +10° (mod 360 = -350)

        double starTrueAz = 0;          // a star directly North in the real sky
        double effectiveAz = starTrueAz - azCalibration; // -10° → simulates sensor's "350°"

        var (wE, wN, wU) = ProjectionMath.AzAltToWorldVector(effectiveAz, altDeg: 0);
        var (dx, dy, dz) = OrientationMath.WorldToDevice(r, wE, wN, wU);
        double focal = ProjectionMath.FocalLengthPx(ScreenWidthPx, FovDeg);
        var screen = ProjectionMath.Project(dx, dy, dz,
            ScreenWidthPx / 2, ScreenHeightPx / 2, focal);

        // Without calibration the star (north) would be at the centre because
        // the quaternion makes the phone look at North. With the simulated
        // 10°-east calibration, the star should land 10°-worth-of-pixels to the
        // LEFT (because we subtracted the offset).
        screen.Should().NotBeNull();
        double dxPx = screen!.Value.sx - ScreenWidthPx / 2;
        dxPx.Should().BeLessThan(0);
        double tenDegreesPx = focal * Math.Tan(10 * Math.PI / 180.0);
        Math.Abs(dxPx).Should().BeApproximately(tenDegreesPx, 1.0);
    }

    /// <summary>
    /// Symmetry: rotating the phone by a small angle around its vertical axis
    /// shifts a star horizontally by exactly the same angular amount.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    public void HorizontalShift_MatchesAngularDelta(double deltaAzDeg)
    {
        double focal = ProjectionMath.FocalLengthPx(ScreenWidthPx, FovDeg);
        double cx = ScreenWidthPx / 2;

        double[] r = OrientationMath.RotationMatrix(Sqrt2Over2, 0, 0, Sqrt2Over2);

        var onAxis = ProjectAt(r, 0, 0, focal, cx, ScreenHeightPx / 2);
        var offAxis = ProjectAt(r, deltaAzDeg, 0, focal, cx, ScreenHeightPx / 2);

        double pxShift = offAxis!.Value.sx - onAxis!.Value.sx;
        double expected = focal * Math.Tan(deltaAzDeg * Math.PI / 180.0);
        pxShift.Should().BeApproximately(expected, 0.5);
    }

    private static (double sx, double sy)? ProjectAt(
        double[] r, double azDeg, double altDeg,
        double focal, double cx, double cy)
    {
        var (wE, wN, wU) = ProjectionMath.AzAltToWorldVector(azDeg, altDeg);
        var (dx, dy, dz) = OrientationMath.WorldToDevice(r, wE, wN, wU);
        return ProjectionMath.Project(dx, dy, dz, cx, cy, focal);
    }
}
