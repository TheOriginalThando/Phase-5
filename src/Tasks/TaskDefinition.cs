using Phase5.Shared;

namespace Phase5.Tasks;

/// <summary>A point the car should pass through, in the global frame.</summary>
public record Waypoint(double X, double Y);

/// <summary>One stage of (possibly staged) evolution: its own waypoints, chromosome length and generations.</summary>
public record StagePlan(
    string Name,
    int Genes,
    int Generations,
    IReadOnlyList<Waypoint> Waypoints,
    // Heading of the curve at its end (global frame), for the optional heading term. NaN if undefined.
    double EndHeading);

/// <summary>A task turned into concrete waypoints and stages (Phase 5 report §4.2 and §4.5).</summary>
public class TaskDefinition
{
    public required string Name { get; init; }
    public required Location Start { get; init; }
    /// <summary>The full set of waypoints (last stage), global frame.</summary>
    public required IReadOnlyList<Waypoint> Waypoints { get; init; }
    public required IReadOnlyList<StagePlan> Stages { get; init; }
    /// <summary>Dense samples of the reference curve (global frame), for plotting. Empty for task 1.</summary>
    public required IReadOnlyList<Waypoint> ReferenceCurve { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }

    public static TaskDefinition Create(RunConfig cfg)
    {
        var start = new Location(cfg.StartX, cfg.StartY, Geometry.WrapAngle(cfg.StartTheta));
        var warnings = new List<string>();

        string name;
        Func<double, (double X, double Y)>? curve; // start frame, parameter u in [0, 1]
        switch (cfg.Task)
        {
            case 1:
                name = "task1";
                curve = null;
                double dist = Math.Sqrt(cfg.Target.X * cfg.Target.X + cfg.Target.Y * cfg.Target.Y);
                if (dist < 1.0) warnings.Add($"Task 1 target is {dist:F2} m from the start; the brief requires at least 1 m.");
                break;
            case 2:
                name = "task2";
                curve = Curves.Sine(cfg.Sine);
                if (cfg.Sine.Length < 2.0) warnings.Add($"Sine length {cfg.Sine.Length:F2} m: the brief requires the path to end at least 2 m away.");
                double kSine = Curves.SineMaxCurvature(cfg.Sine);
                if (kSine > 1.0 / cfg.MinTurnRadius)
                    warnings.Add($"Sine is too tight: sharpest curvature {kSine:F2} /m (radius {1 / kSine:F2} m) " +
                                 $"is tighter than the car's minimum turning radius {cfg.MinTurnRadius:F2} m.");
                break;
            default:
                name = "task3";
                curve = Curves.FigureEight(cfg.Eight);
                break;
        }

        // Numerical curvature check for any curve (covers the figure eight too).
        if (curve != null)
        {
            double kMax = Curves.MaxCurvature(curve);
            if (kMax > 1.0 / cfg.MinTurnRadius && cfg.Task != 2) // task 2 already reported analytically
                warnings.Add($"{name}: sharpest curvature {kMax:F2} /m (radius {1 / kMax:F2} m) is tighter than " +
                             $"the car's minimum turning radius {cfg.MinTurnRadius:F2} m.");
        }

        var stages = new List<StagePlan>();
        foreach (StageConfig s in cfg.Stages)
        {
            List<Waypoint> wps;
            double endHeading;
            if (curve == null)
            {
                wps = new() { ToGlobal(start, cfg.Target.X, cfg.Target.Y) };
                endHeading = double.NaN;
            }
            else
            {
                wps = Curves.Resample(curve, s.PathFraction, cfg.WaypointSpacing)
                    .Select(p => ToGlobal(start, p.X, p.Y)).ToList();
                endHeading = Geometry.WrapAngle(start.Theta + Curves.TangentAngle(curve, s.PathFraction));
            }
            stages.Add(new StagePlan(s.Name, s.Genes, s.Generations, wps, endHeading));
        }

        foreach (Waypoint w in stages.SelectMany(s => s.Waypoints))
            if (!cfg.Bounds.Contains(w.X, w.Y, cfg.Bounds.Margin))
                throw new InvalidDataException(
                    $"{name}: waypoint ({w.X:F2}, {w.Y:F2}) is outside the track bounds (margin {cfg.Bounds.Margin} m). " +
                    "Move the start pose or shrink the task.");

        var reference = curve == null
            ? new List<Waypoint>()
            : Enumerable.Range(0, 401).Select(i => curve(i / 400.0)).Select(p => ToGlobal(start, p.X, p.Y)).ToList();

        return new TaskDefinition
        {
            Name = name,
            Start = start,
            Waypoints = stages[^1].Waypoints,
            Stages = stages,
            ReferenceCurve = reference,
            Warnings = warnings,
        };
    }

    private static Waypoint ToGlobal(Location start, double x, double y)
    {
        Location g = Geometry.ToGlobal(start, new Location(x, y, 0));
        return new Waypoint(g.X, g.Y);
    }
}
