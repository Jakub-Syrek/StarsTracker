namespace StarsTracker.Services;

/// <summary>
/// Pure math for converting Android <c>TYPE_ROTATION_VECTOR</c> quaternions
/// (which rotate from the device frame to the world frame X=East, Y=North,
/// Z=Up) into azimuth and altitude of the rear-camera pointing direction.
/// </summary>
public static class OrientationMath
{
    /// <summary>
    /// Returns the rear camera's pointing direction (rear of phone = -Z device)
    /// expressed in the world frame.
    /// </summary>
    public static (double east, double north, double up) CameraDirectionWorld(
        double qx, double qy, double qz, double qw)
    {
        // R(q) rotates device → world. Camera points along -Z device, so its
        // world-frame direction is R * (0,0,-1) = -column_2(R) = (-m02, -m12, -m22).
        double m02 = 2 * (qx * qz + qy * qw);
        double m12 = 2 * (qy * qz - qx * qw);
        double m22 = 1 - 2 * (qx * qx + qy * qy);
        return (-m02, -m12, -m22);
    }

    /// <summary>
    /// Azimuth (compass bearing 0–360, CW from North) and altitude (degrees,
    /// 0=horizon, 90=zenith) of the rear camera given the device quaternion.
    /// </summary>
    public static (double azimuthDeg, double altitudeDeg) AzimuthAltitude(
        double qx, double qy, double qz, double qw)
    {
        var (vE, vN, vU) = CameraDirectionWorld(qx, qy, qz, qw);
        double azRad = Math.Atan2(vE, vN);
        double azDeg = (azRad * 180.0 / Math.PI + 360.0) % 360.0;
        double altDeg = Math.Asin(Math.Clamp(vU, -1.0, 1.0)) * 180.0 / Math.PI;
        return (azDeg, altDeg);
    }

    /// <summary>
    /// Builds the 3×3 rotation matrix R(q) (device → world) as a flattened
    /// row-major array of length 9. Useful for the inverse transform via
    /// transpose when projecting world vectors into the device frame.
    /// </summary>
    public static double[] RotationMatrix(double qx, double qy, double qz, double qw)
    {
        return
        [
            1 - 2 * (qy * qy + qz * qz),  2 * (qx * qy - qz * qw),      2 * (qx * qz + qy * qw),
            2 * (qx * qy + qz * qw),      1 - 2 * (qx * qx + qz * qz),  2 * (qy * qz - qx * qw),
            2 * (qx * qz - qy * qw),      2 * (qy * qz + qx * qw),      1 - 2 * (qx * qx + qy * qy),
        ];
    }

    /// <summary>
    /// Transforms a world-frame vector (E, N, Up) into the device frame
    /// (right, top, out-of-screen). Uses R(q)^T because R(q) goes the
    /// other way per Android sensor convention.
    /// </summary>
    public static (double dx, double dy, double dz) WorldToDevice(
        double[] r, double wE, double wN, double wU)
    {
        if (r.Length != 9) throw new ArgumentException("expected 9 elements", nameof(r));
        // r is row-major R; multiplying by R^T means: device.x = column_0(R) · world
        return (
            r[0] * wE + r[3] * wN + r[6] * wU,
            r[1] * wE + r[4] * wN + r[7] * wU,
            r[2] * wE + r[5] * wN + r[8] * wU);
    }

    /// <summary>
    /// Spherical linear interpolation between two unit quaternions. Used to
    /// smooth a noisy stream of orientation samples — the filtered quaternion
    /// at frame N is <c>Slerp(prev, raw, alpha)</c> with a small alpha
    /// (e.g. 0.2) to suppress sensor jitter without visible lag.
    /// </summary>
    /// <param name="ax">prev quaternion x</param>
    /// <param name="ay">prev quaternion y</param>
    /// <param name="az">prev quaternion z</param>
    /// <param name="aw">prev quaternion w</param>
    /// <param name="bx">target quaternion x</param>
    /// <param name="by">target quaternion y</param>
    /// <param name="bz">target quaternion z</param>
    /// <param name="bw">target quaternion w</param>
    /// <param name="t">interpolation factor in [0, 1] — 0 keeps prev, 1 jumps to target</param>
    public static (double x, double y, double z, double w) Slerp(
        double ax, double ay, double az, double aw,
        double bx, double by, double bz, double bw,
        double t)
    {
        double dot = ax * bx + ay * by + az * bz + aw * bw;
        // If dot is negative, the quaternions are on opposite hemispheres —
        // negate one to take the shorter great-circle path.
        if (dot < 0)
        {
            bx = -bx; by = -by; bz = -bz; bw = -bw;
            dot = -dot;
        }

        // For nearly-collinear quaternions, fall back to normalized lerp to
        // avoid the divide-by-near-zero in the sin-based formula.
        const double dotThreshold = 0.9995;
        double rx, ry, rz, rw;
        if (dot > dotThreshold)
        {
            rx = ax + t * (bx - ax);
            ry = ay + t * (by - ay);
            rz = az + t * (bz - az);
            rw = aw + t * (bw - aw);
        }
        else
        {
            double theta0 = Math.Acos(Math.Clamp(dot, -1.0, 1.0));
            double theta = theta0 * t;
            double sinTheta = Math.Sin(theta);
            double sinTheta0 = Math.Sin(theta0);
            double s1 = Math.Cos(theta) - dot * sinTheta / sinTheta0;
            double s2 = sinTheta / sinTheta0;
            rx = s1 * ax + s2 * bx;
            ry = s1 * ay + s2 * by;
            rz = s1 * az + s2 * bz;
            rw = s1 * aw + s2 * bw;
        }

        // Normalize to combat accumulated floating-point drift across calls.
        double norm = Math.Sqrt(rx * rx + ry * ry + rz * rz + rw * rw);
        if (norm < 1e-12) return (0, 0, 0, 1);
        return (rx / norm, ry / norm, rz / norm, rw / norm);
    }
}
