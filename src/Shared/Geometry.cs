namespace Phase5.Shared;

public static class Geometry
{
    /// <summary>Wraps an angle to [-π, π].</summary>
    public static double WrapAngle(double angle) => Math.Atan2(Math.Sin(angle), Math.Cos(angle));

    /// <summary>Applies a motion given in the car's local frame to a global pose.</summary>
    public static Location ApplyLocalDelta(Location loc, double dxLocal, double dyLocal, double dTheta)
    {
        double c = Math.Cos(loc.Theta), s = Math.Sin(loc.Theta);
        return new Location(
            loc.X + dxLocal * c - dyLocal * s,
            loc.Y + dxLocal * s + dyLocal * c,
            WrapAngle(loc.Theta + dTheta));
    }

    /// <summary>Expresses a pose given relative to <paramref name="frame"/> in the global frame.</summary>
    public static Location ToGlobal(Location frame, Location relative) =>
        ApplyLocalDelta(frame, relative.X, relative.Y, relative.Theta);

    /// <summary>Expresses a global pose relative to <paramref name="frame"/> (inverse of <see cref="ToGlobal"/>).</summary>
    public static Location ToFrame(Location frame, Location global)
    {
        double dx = global.X - frame.X, dy = global.Y - frame.Y;
        double c = Math.Cos(frame.Theta), s = Math.Sin(frame.Theta);
        return new Location(dx * c + dy * s, -dx * s + dy * c, WrapAngle(global.Theta - frame.Theta));
    }

    public static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2, dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
