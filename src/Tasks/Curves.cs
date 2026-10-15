namespace Phase5.Tasks;

/// <summary>Reference curves in the start frame (start at the origin, heading along +x),
/// parameterised by u in [0, 1].</summary>
public static class Curves
{
    private const int DenseSamples = 4000;

    /// <summary>y = A·sin(2πx/λ) for x in [0, L].</summary>
    public static Func<double, (double X, double Y)> Sine(SineConfig c) => u =>
    {
        double x = u * c.Length;
        return (x, c.Amplitude * Math.Sin(2 * Math.PI * x / c.Wavelength));
    };

    /// <summary>Sharpest curvature of the sine, A·(2π/λ)², at its peaks.</summary>
    public static double SineMaxCurvature(SineConfig c) =>
        c.Amplitude * Math.Pow(2 * Math.PI / c.Wavelength, 2);

    /// <summary>Figure eight starting and ending at its crossing point (the origin), rotated so the starting
    /// tangent points along +x. u in [0, 0.5] is the first lobe. Shapes (config eight.shape):
    /// "gerono"    x = a·sin t, y = (b/2)·sin 2t (Phase 5 report §4.2);
    /// "bernoulli" x = a·cos t / (1 + sin²t), y = a·sin t·cos t / (1 + sin²t), half-width a (b unused).</summary>
    public static Func<double, (double X, double Y)> FigureEight(FigureEightConfig c)
    {
        Func<double, (double X, double Y)> raw = c.Shape.ToLowerInvariant() switch
        {
            "gerono" => u =>
            {
                double t = 2 * Math.PI * u;
                return (c.A * Math.Sin(t), 0.5 * c.B * Math.Sin(2 * t));
            },
            "bernoulli" => u =>
            {
                double t = Math.PI / 2 + 2 * Math.PI * u; // t = π/2 is the crossing point
                double s = Math.Sin(t), d = 1 + s * s;
                return (c.A * Math.Cos(t) / d, c.A * s * Math.Cos(t) / d);
            },
            _ => throw new InvalidDataException($"Unknown figure-eight shape '{c.Shape}' (use 'gerono' or 'bernoulli')."),
        };

        double rot = -TangentAngle(raw, 0);
        double cr = Math.Cos(rot), sr = Math.Sin(rot);
        return u =>
        {
            var (x, y) = raw(u);
            return (x * cr - y * sr, x * sr + y * cr);
        };
    }

    /// <summary>Points spaced roughly <paramref name="spacing"/> apart along the curve for u in (0, uEnd].
    /// The start point is left out; the end point is always included.</summary>
    public static List<(double X, double Y)> Resample(Func<double, (double X, double Y)> curve, double uEnd, double spacing)
    {
        int n = Math.Max(10, (int)(DenseSamples * uEnd));
        var pts = new List<(double X, double Y)>();
        double travelled = 0, next = spacing;
        var prev = curve(0);
        for (int i = 1; i <= n; i++)
        {
            var p = curve(uEnd * i / n);
            travelled += Math.Sqrt((p.X - prev.X) * (p.X - prev.X) + (p.Y - prev.Y) * (p.Y - prev.Y));
            if (travelled >= next)
            {
                pts.Add(p);
                next += spacing;
            }
            prev = p;
        }

        var end = curve(uEnd);
        // Snap the last point to the exact end, or add the end if the gap is big enough.
        if (pts.Count > 0 && travelled - (next - spacing) < 0.5 * spacing)
            pts[^1] = end;
        else
            pts.Add(end);
        return pts;
    }

    /// <summary>Direction of travel along the curve at parameter u (radians, start frame).</summary>
    public static double TangentAngle(Func<double, (double X, double Y)> curve, double u)
    {
        const double h = 1e-5;
        var a = curve(Math.Max(0, u - h));
        var b = curve(Math.Min(1, u + h));
        return Math.Atan2(b.Y - a.Y, b.X - a.X);
    }

    /// <summary>Largest curvature along the whole curve, by finite differences.</summary>
    public static double MaxCurvature(Func<double, (double X, double Y)> curve)
    {
        double kMax = 0;
        double h = 1.0 / DenseSamples;
        for (int i = 1; i < DenseSamples; i++)
        {
            var p0 = curve((i - 1) * h);
            var p1 = curve(i * h);
            var p2 = curve((i + 1) * h);
            double dx = (p2.X - p0.X) / (2 * h), dy = (p2.Y - p0.Y) / (2 * h);
            double ddx = (p2.X - 2 * p1.X + p0.X) / (h * h), ddy = (p2.Y - 2 * p1.Y + p0.Y) / (h * h);
            double speed = Math.Sqrt(dx * dx + dy * dy);
            if (speed < 1e-9) continue;
            kMax = Math.Max(kMax, Math.Abs(dx * ddy - dy * ddx) / (speed * speed * speed));
        }
        return kMax;
    }
}
