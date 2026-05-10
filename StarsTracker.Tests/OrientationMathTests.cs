using StarsTracker.Services;

namespace StarsTracker.Tests;

/// <summary>
/// Validates the Android-quaternion → camera-direction conversion against
/// hand-calculated reference orientations:
///   - Identity quaternion: phone flat on a table, camera looks down.
///   - 90° around X: phone vertical with top edge up, camera at the horizon
///     pointing North.
///   - 90° around Y: phone in landscape, camera at horizon pointing West.
///   - 90° around Z: phone flat with top edge pointing West, camera looks down.
/// </summary>
public sealed class OrientationMathTests
{
    private const double DegTolerance = 1e-6;
    private const double VectorTolerance = 1e-9;
    private static readonly double Sqrt2Over2 = Math.Sqrt(2) / 2;

    [Fact]
    public void IdentityQuaternion_CameraLooksStraightDown()
    {
        var (e, n, u) = OrientationMath.CameraDirectionWorld(0, 0, 0, 1);
        e.Should().BeApproximately(0, VectorTolerance);
        n.Should().BeApproximately(0, VectorTolerance);
        u.Should().BeApproximately(-1, VectorTolerance);

        var (az, alt) = OrientationMath.AzimuthAltitude(0, 0, 0, 1);
        alt.Should().BeApproximately(-90, DegTolerance);
        az.Should().BeInRange(0, 360);
    }

    [Fact]
    public void NinetyAroundX_PhoneUpright_CameraAtHorizonNorth()
    {
        // Quaternion (sin(45), 0, 0, cos(45)) = +90° around X axis
        var (e, n, u) = OrientationMath.CameraDirectionWorld(Sqrt2Over2, 0, 0, Sqrt2Over2);
        e.Should().BeApproximately(0, VectorTolerance);
        n.Should().BeApproximately(1, VectorTolerance);
        u.Should().BeApproximately(0, VectorTolerance);

        var (az, alt) = OrientationMath.AzimuthAltitude(Sqrt2Over2, 0, 0, Sqrt2Over2);
        az.Should().BeApproximately(0, DegTolerance);
        alt.Should().BeApproximately(0, DegTolerance);
    }

    [Fact]
    public void NinetyAroundY_PhoneLandscape_CameraLooksWest()
    {
        var (e, n, u) = OrientationMath.CameraDirectionWorld(0, Sqrt2Over2, 0, Sqrt2Over2);
        e.Should().BeApproximately(-1, VectorTolerance);
        n.Should().BeApproximately(0, VectorTolerance);
        u.Should().BeApproximately(0, VectorTolerance);

        var (az, alt) = OrientationMath.AzimuthAltitude(0, Sqrt2Over2, 0, Sqrt2Over2);
        az.Should().BeApproximately(270, DegTolerance);
        alt.Should().BeApproximately(0, DegTolerance);
    }

    [Fact]
    public void NinetyAroundZ_PhoneFlatRotatedCW_CameraStillLooksDown()
    {
        var (e, n, u) = OrientationMath.CameraDirectionWorld(0, 0, Sqrt2Over2, Sqrt2Over2);
        e.Should().BeApproximately(0, VectorTolerance);
        n.Should().BeApproximately(0, VectorTolerance);
        u.Should().BeApproximately(-1, VectorTolerance);

        var (az, alt) = OrientationMath.AzimuthAltitude(0, 0, Sqrt2Over2, Sqrt2Over2);
        alt.Should().BeApproximately(-90, DegTolerance);
    }

    [Fact]
    public void RotationMatrix_Determinant_IsOne()
    {
        // Pick a non-trivial quaternion (rotation around tilted axis)
        double a = 30 * Math.PI / 180;
        double sx = Math.Sin(a) / Math.Sqrt(3);
        double sy = sx;
        double sz = sx;
        double[] r = OrientationMath.RotationMatrix(sx, sy, sz, Math.Cos(a));

        // det(R) = m00(m11*m22 - m12*m21) - m01(m10*m22 - m12*m20) + m02(m10*m21 - m11*m20)
        double det =
              r[0] * (r[4] * r[8] - r[5] * r[7])
            - r[1] * (r[3] * r[8] - r[5] * r[6])
            + r[2] * (r[3] * r[7] - r[4] * r[6]);

        det.Should().BeApproximately(1, 1e-9, "rotation matrices have determinant 1");
    }

    [Fact]
    public void WorldToDevice_PreservesVectorLength()
    {
        double[] r = OrientationMath.RotationMatrix(0.3, 0.4, 0.5, 0.7071);
        // normalize
        double norm = Math.Sqrt(0.3 * 0.3 + 0.4 * 0.4 + 0.5 * 0.5 + 0.7071 * 0.7071);
        double qx = 0.3 / norm, qy = 0.4 / norm, qz = 0.5 / norm, qw = 0.7071 / norm;
        r = OrientationMath.RotationMatrix(qx, qy, qz, qw);

        double wE = 1, wN = 2, wU = 3;
        double inputLen = Math.Sqrt(wE * wE + wN * wN + wU * wU);
        var (dx, dy, dz) = OrientationMath.WorldToDevice(r, wE, wN, wU);
        double outputLen = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        outputLen.Should().BeApproximately(inputLen, 1e-9);
    }

    [Fact]
    public void WorldToDevice_OnIdentity_IsPassThrough()
    {
        double[] r = OrientationMath.RotationMatrix(0, 0, 0, 1);
        var (dx, dy, dz) = OrientationMath.WorldToDevice(r, 7, 11, 13);
        dx.Should().BeApproximately(7, VectorTolerance);
        dy.Should().BeApproximately(11, VectorTolerance);
        dz.Should().BeApproximately(13, VectorTolerance);
    }

    [Fact]
    public void WorldToDevice_Rejects_WrongMatrixSize()
    {
        var act = () => OrientationMath.WorldToDevice(new double[8], 0, 0, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0.7071, 0, 0, 0.7071)]
    [InlineData(0, 0.7071, 0, 0.7071)]
    [InlineData(0.5, 0.5, 0.5, 0.5)]
    [InlineData(-0.7071, 0, 0, 0.7071)]
    public void AzimuthAltitude_AlwaysProducesValidRanges(double qx, double qy, double qz, double qw)
    {
        var (az, alt) = OrientationMath.AzimuthAltitude(qx, qy, qz, qw);
        az.Should().BeInRange(0, 360);
        alt.Should().BeInRange(-90, 90);
    }
}
