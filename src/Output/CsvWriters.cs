using System.Globalization;
using System.Text;
using Phase5.Evolution;
using Phase5.Shared;
using Phase5.Tasks;

namespace Phase5.Output;

/// <summary>All files a run writes (CLAUDE.md §9). Numbers always use the invariant culture.</summary>
public static class CsvWriters
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static string F(double v) => v.ToString("R", Inv);

    /// <summary>Shortest round-trip string of the value as a 32-bit float, matching the lecturer's example.
    /// Never uses exponent notation (e.g. 1E-05) and never writes "-0". Rounding to 1e-9 first removes
    /// floating-point noise such as 6E-18 left over from frame rotations.</summary>
    public static string F32(double v)
    {
        float f = (float)Math.Round(v, 9);
        if (f == 0) return "0";
        string s = f.ToString(Inv);
        return s.Contains('E') ? f.ToString("0.####################", Inv) : s;
    }

    private static void WriteLines(string path, IEnumerable<string> lines)
    {
        using var w = new StreamWriter(path, append: false, Utf8NoBom) { NewLine = "\r\n" };
        foreach (string line in lines) w.WriteLine(line);
    }

    public static void WriteFitnessLog(string path, IEnumerable<GenerationStats> stats) =>
        WriteLines(path, stats.Select(s => string.Join(",",
                s.Generation.ToString(Inv), s.Stage.ToString(Inv), F(s.Best), F(s.Mean), F(s.Std),
                F(s.BestTerms.W), F(s.BestTerms.EEnd), F(s.BestTerms.S), F(s.BestTerms.OutOfBoundsFraction)))
            .Prepend("generation,stage,best,mean,std,best_w,best_e_end,best_s,best_oob"));

    public static void WritePath(string path, IEnumerable<Location> locations) =>
        WriteLines(path, locations.Select(l => $"{F(l.X)},{F(l.Y)},{F(l.Theta)}").Prepend("x,y,theta"));

    public static void WriteWaypoints(string path, IEnumerable<Waypoint> waypoints) =>
        WriteLines(path, waypoints.Select(w => $"{F(w.X)},{F(w.Y)}").Prepend("x,y"));

    /// <summary>The Phase 6 deliverable, in the lecturer's exact layout (CLAUDE.md §9): no header, one row per
    /// timestep, "x,y,θ,τ,σ" where the pose is the predicted pose after that row's command, relative to the start pose.</summary>
    /// <param name="path">Simulated path: N+1 global poses, starting with the start pose.</param>
    public static void WriteSolution(string file, IReadOnlyList<Command> commands, IReadOnlyList<Location> path, Location start)
    {
        if (path.Count != commands.Count + 1)
            throw new ArgumentException($"Expected {commands.Count + 1} path points for {commands.Count} commands, got {path.Count}.");

        WriteLines(file, commands.Select((c, i) =>
        {
            Location p = Geometry.ToFrame(start, path[i + 1]);
            // Throttle (τ) before steering (σ): the opposite order to our Command record.
            return string.Join(",", F32(p.X), F32(p.Y), F32(p.Theta), F32(c.Throttle), F32(c.Steering));
        }));
    }

    public static void WriteText(string path, string text) => File.WriteAllText(path, text, Utf8NoBom);
}
