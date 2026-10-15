using Phase5.Shared;
using Phase5.Tasks;

namespace Phase5.Fitness;

/// <summary>The terms of F, kept separately so they can be logged and compared in the report.</summary>
public record FitnessResult(double Total, double W, double EEnd, double S, double OutOfBoundsFraction, double HeadingError);

/// <summary>F = W + w_end·E_end + λ_s·S + p_oob·(fraction out of bounds) [+ w_head·heading error]. Lower is better (Phase 5 report §4.3).</summary>
public class FitnessFunction
{
    private readonly FitnessConfig weights;
    private readonly CommandLimits limits;
    private readonly TrackBounds bounds;

    public FitnessFunction(FitnessConfig weights, CommandLimits limits, TrackBounds bounds)
    {
        this.weights = weights;
        this.limits = limits;
        this.bounds = bounds;
    }

    /// <param name="genes">The chromosome (unexpanded), used for the smoothness term.</param>
    /// <param name="path">Simulated path, N+1 locations starting at the start pose.</param>
    public FitnessResult Evaluate(IReadOnlyList<Command> genes, IReadOnlyList<Location> path, StagePlan stage)
    {
        double w = OrderedWaypointError(path, stage.Waypoints);

        Waypoint goal = stage.Waypoints[^1];
        Location end = path[^1];
        double eEnd = Geometry.Distance(end.X, end.Y, goal.X, goal.Y);

        double s = Smoothness(genes, limits);
        double oob = OutOfBoundsFraction(path, bounds);

        double head = 0;
        if (weights.WHead != 0 && !double.IsNaN(stage.EndHeading))
            head = Math.Abs(Geometry.WrapAngle(end.Theta - stage.EndHeading));

        double total = w + weights.WEnd * eEnd + weights.LambdaS * s + weights.POob * oob + weights.WHead * head;
        return new FitnessResult(total, w, eEnd, s, oob, head);
    }

    /// <summary>Mean distance from each waypoint to its nearest path point, searched in order:
    /// waypoint k only considers path points at or after the index matched to waypoint k-1.</summary>
    public static double OrderedWaypointError(IReadOnlyList<Location> path, IReadOnlyList<Waypoint> waypoints)
    {
        double sum = 0;
        int from = 0;
        foreach (Waypoint wp in waypoints)
        {
            double best = double.MaxValue;
            int bestIdx = from;
            for (int j = from; j < path.Count; j++)
            {
                double d = Geometry.Distance(path[j].X, path[j].Y, wp.X, wp.Y);
                if (d < best) { best = d; bestIdx = j; }
            }
            sum += best;
            from = bestIdx;
        }
        return sum / waypoints.Count;
    }

    /// <summary>Mean normalised change between consecutive genes. Steering is divided by its full range (1.2)
    /// and throttle by 0.4, as in Phase 5 report §4.3.</summary>
    public static double Smoothness(IReadOnlyList<Command> genes, CommandLimits limits)
    {
        if (genes.Count < 2) return 0;
        // The S formula fixes the throttle divisor at 0.4, the brief's full throttle range [0, 0.4].
        const double throttleScale = 0.4;
        double sum = 0;
        for (int i = 1; i < genes.Count; i++)
            sum += Math.Abs(genes[i].Steering - genes[i - 1].Steering) / limits.SteeringRange
                 + Math.Abs(genes[i].Throttle - genes[i - 1].Throttle) / throttleScale;
        return sum / (genes.Count - 1);
    }

    public static double OutOfBoundsFraction(IReadOnlyList<Location> path, TrackBounds bounds)
    {
        int outside = 0;
        foreach (Location p in path)
            if (!bounds.Contains(p.X, p.Y)) outside++;
        return (double)outside / path.Count;
    }
}
