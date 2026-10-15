using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Phase5.Evolution;
using Phase5.Fitness;
using Phase5.Output;
using Phase5.Shared;
using Phase5.Simulation;
using Phase5.Tasks;

namespace Phase5.Runner;

public static class Program
{
    private const string Usage = """
        Usage:
          Phase5 run     --task <1|2|3> [--config <file>] [--seed <k> | --seeds <a-b|a,b,c>]
                         [--experiment <name>] [--set key=value ...] [--out <dir>]
          Phase5 measure [--config <file>] [--set key=value ...]

        run      Evolves a solution. Writes results/<task>/<experiment>/seed<k>/.
        measure  Sanity-checks the simulator and measures speed, turning radius and evaluation time (Phase 5 report §3.2).
        --config defaults to configs/task<N>.json. --set overrides any config value, e.g. --set fitness.lambda_s=0
        """;

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help") { Console.WriteLine(Usage); return 0; }
            var opts = Options.Parse(args.Skip(1).ToArray());
            return args[0] switch
            {
                "run" => Run(opts),
                "measure" => Measure(opts),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.\n\n{Usage}"),
            };
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return 1;
        }
    }

    private static int Run(Options o)
    {
        string configPath = o.Config ?? $"configs/task{o.Task ?? throw new ArgumentException("--task or --config is required.")}.json";
        RunConfig baseCfg = RunConfig.Load(configPath, o.Overrides);
        if (o.Task is int t && t != baseCfg.Task)
            throw new ArgumentException($"--task {t} does not match task {baseCfg.Task} in {configPath}.");
        if (o.Experiment != null) baseCfg.Experiment = o.Experiment;

        List<int> seeds = o.Seeds ?? new List<int> { baseCfg.Seed };
        var summaries = new List<RunSummary>();
        foreach (int seed in seeds)
        {
            RunConfig cfg = RunConfig.Load(configPath, o.Overrides);
            cfg.Experiment = baseCfg.Experiment;
            cfg.Seed = seed;
            summaries.Add(RunOne(cfg, o.OutRoot ?? "results"));
        }

        if (summaries.Count > 1)
        {
            double mean = summaries.Average(s => s.FinalFitness);
            double sd = Math.Sqrt(summaries.Average(s => Math.Pow(s.FinalFitness - mean, 2)));
            Console.WriteLine($"\n{summaries.Count} seeds: final fitness mean {mean:F4}, std {sd:F4}, " +
                              $"best {summaries.Min(s => s.FinalFitness):F4}, worst {summaries.Max(s => s.FinalFitness):F4}");
        }
        return 0;
    }

    private static RunSummary RunOne(RunConfig cfg, string outRoot)
    {
        TaskDefinition task = TaskDefinition.Create(cfg);
        string dir = Path.Combine(outRoot, task.Name, cfg.Experiment, $"seed{cfg.Seed}");
        Directory.CreateDirectory(dir);

        Console.WriteLine($"== {task.Name} / {cfg.Experiment} / seed {cfg.Seed}  (simulator: {cfg.Simulator.Kind}, " +
                          $"stages: {string.Join(" -> ", task.Stages.Select(s => $"{s.Name} {s.Genes} genes x {s.Generations} gens"))})");
        foreach (string w in task.Warnings) Console.WriteLine($"WARNING: {w}");

        ISimulator sim = SimulatorFactory.Create(cfg);
        try
        {
            var fitness = new FitnessFunction(cfg.Fitness, cfg.Limits, cfg.Bounds);
            var engine = new EvolutionEngine(cfg, sim, task, fitness);
            var log = new List<GenerationStats>();
            EvolutionResult result = engine.Run(s =>
            {
                log.Add(s);
                if (s.Generation % 25 == 0)
                    Console.WriteLine($"  gen {s.Generation,4} [{s.StageName}]  best {s.Best:F4}  mean {s.Mean:F4}  " +
                                      $"(W {s.BestTerms.W:F3}, E_end {s.BestTerms.EEnd:F3}, S {s.BestTerms.S:F3}, oob {s.BestTerms.OutOfBoundsFraction:P0})");
            });

            // Final fitness against the last stage (what the whole task is judged on).
            FitnessResult final = fitness.Evaluate(result.Best.Genes, result.Path, task.Stages[^1]);

            CsvWriters.WriteFitnessLog(Path.Combine(dir, "fitness_log.csv"), log);
            CsvWriters.WritePath(Path.Combine(dir, "best_path.csv"), result.Path);
            CsvWriters.WriteWaypoints(Path.Combine(dir, "waypoints.csv"), task.Waypoints);
            CsvWriters.WriteWaypoints(Path.Combine(dir, "reference_curve.csv"), task.ReferenceCurve);
            CsvWriters.WriteSolution(Path.Combine(dir, "solution.csv"), result.Commands, result.Path, task.Start);
            CsvWriters.WriteText(Path.Combine(dir, "config.json"), cfg.ToJson());

            Location end = result.Path[^1];
            var summary = new RunSummary(
                task.Name, cfg.Experiment, cfg.Seed, final.Total, final.W, final.EEnd, final.S, final.OutOfBoundsFraction,
                final.HeadingError, result.Commands.Count, result.Evaluations, result.Elapsed.TotalSeconds,
                Geometry.Distance(end.X, end.Y, task.Start.X, task.Start.Y),
                result.Path.Max(p => Geometry.Distance(p.X, p.Y, task.Start.X, task.Start.Y)),
                task.Warnings.ToList());
            CsvWriters.WriteText(Path.Combine(dir, "summary.json"), JsonSerializer.Serialize(summary, RunConfig.JsonOptions));

            Console.WriteLine($"  final {final.Total:F4}  (W {final.W:F3} m, E_end {final.EEnd:F3} m, S {final.S:F3}, " +
                              $"oob {final.OutOfBoundsFraction:P0})  end is {summary.EndDistanceFromStart:F2} m from start, " +
                              $"{result.Evaluations} evaluations in {result.Elapsed.TotalSeconds:F1} s");
            Console.WriteLine($"  -> {dir}");
            return summary;
        }
        finally
        {
            (sim as IDisposable)?.Dispose();
        }
    }

    private record RunSummary(
        string Task, string Experiment, int Seed, double FinalFitness, double W, double EEnd, double S,
        double OutOfBoundsFraction, double HeadingError, int Commands, long Evaluations, double Seconds,
        double EndDistanceFromStart, double MaxDistanceFromStart, List<string> Warnings);

    /// <summary>Phase 5 report §3.2: straight line and circles, speed, turning radius, timing.</summary>
    private static int Measure(Options o)
    {
        RunConfig cfg = RunConfig.Load(o.Config ?? "configs/task3.json", o.Overrides);
        ISimulator sim = SimulatorFactory.Create(cfg);
        try
        {
            var start = new Location(0, 0, 0);
            double dt = 0.2;
            CommandLimits lim = cfg.Limits;
            Console.WriteLine($"Simulator: {cfg.Simulator.Kind}. Throttle range [{lim.ThrottleMin}, {lim.ThrottleMax}], mid {lim.ThrottleMid:F3}.\n");

            // Straight line at each throttle.
            Console.WriteLine("Straight (steering 0), 30 steps:");
            double vNominal = 0;
            foreach (double tau in new[] { lim.ThrottleMin, lim.ThrottleMid, lim.ThrottleMax })
            {
                List<Location> p = sim.StepSequence(Enumerable.Repeat(new Command(0, tau), 30).ToList(), start);
                // Steady-state speed from the second half (the first steps include the start from rest).
                double dist = 0;
                for (int i = 15; i < 30; i++) dist += Geometry.Distance(p[i].X, p[i].Y, p[i + 1].X, p[i + 1].Y);
                double v = dist / (15 * dt);
                if (tau == lim.ThrottleMid) vNominal = v;
                Console.WriteLine($"  throttle {tau:F3}: speed {v:F3} m/s, end ({p[^1].X:F2}, {p[^1].Y:F2}), heading drift {p[^1].Theta * 180 / Math.PI:F1} deg");
            }

            // Circles at full lock and at a few smaller steering values.
            Console.WriteLine("\nConstant steering at mid throttle, 60 steps (radius from steady state):");
            double minRadius = double.MaxValue;
            foreach (double sigma in new[] { lim.SteeringMax, lim.SteeringMax / 2, 0.1, -0.1, -lim.SteeringMax / 2, -lim.SteeringMax })
            {
                List<Location> p = sim.StepSequence(Enumerable.Repeat(new Command(sigma, lim.ThrottleMid), 60).ToList(), start);
                double dist = 0, turn = 0;
                for (int i = 30; i < 60; i++)
                {
                    dist += Geometry.Distance(p[i].X, p[i].Y, p[i + 1].X, p[i + 1].Y);
                    turn += Geometry.WrapAngle(p[i + 1].Theta - p[i].Theta);
                }
                double radius = Math.Abs(turn) < 1e-9 ? double.PositiveInfinity : dist / Math.Abs(turn);
                minRadius = Math.Min(minRadius, radius);
                string dir = turn > 0 ? "left" : "right";
                Console.WriteLine($"  steering {sigma,5:F2}: turns {dir,-5} radius {radius:F3} m  ({turn / 30 * 180 / Math.PI:F1} deg/step)");
            }

            // Suggested chromosome lengths, N = ceil(1.2 · L / (v · hold · dt)).
            Console.WriteLine($"\nv_nominal = {vNominal:F3} m/s, minimum turning radius = {minRadius:F3} m");
            Console.WriteLine($"Suggested genes (hold_steps {cfg.HoldSteps}):");
            double stepLen = vNominal * cfg.HoldSteps * dt;
            double l1 = Math.Sqrt(cfg.Target.X * cfg.Target.X + cfg.Target.Y * cfg.Target.Y);
            double l2 = ArcLength(Curves.Sine(cfg.Sine));
            double l3 = ArcLength(Curves.FigureEight(cfg.Eight));
            foreach (var (name, len) in new[] { ("task1 (straight-line distance)", l1), ("task2 sine", l2), ("task3 eight", l3) })
                Console.WriteLine($"  {name,-31} path {len:F2} m -> {Math.Ceiling(1.2 * len / stepLen)} genes");
            Console.WriteLine($"Feasibility: sine sharpest radius {1 / Curves.SineMaxCurvature(cfg.Sine):F2} m, " +
                              $"eight sharpest radius {1 / Curves.MaxCurvature(Curves.FigureEight(cfg.Eight)):F2} m");

            // Timing of one fitness evaluation (simulation dominates).
            int genes = cfg.Stages[^1].Genes;
            var rng = new Random(1);
            var cmds = Enumerable.Range(0, 200).Select(_ => Chromosome.Expand(
                Enumerable.Range(0, genes).Select(_ => new Command((rng.NextDouble() * 2 - 1) * lim.SteeringMax,
                    lim.ThrottleMin + rng.NextDouble() * lim.ThrottleRange)).ToList(), cfg.HoldSteps)).ToList();
            sim.StepSequence(cmds[0], start); // warm up
            var sw = Stopwatch.StartNew();
            foreach (var c in cmds) sim.StepSequence(c, start);
            double serialMs = sw.Elapsed.TotalMilliseconds / cmds.Count;
            sw.Restart();
            Parallel.ForEach(cmds, c => sim.StepSequence(c, start));
            double parallelMs = sw.Elapsed.TotalMilliseconds / cmds.Count;
            long evals = (long)cfg.Ea.Population * cfg.Stages.Sum(s => s.Generations);
            Console.WriteLine($"\nOne evaluation ({genes} genes = {genes * cfg.HoldSteps} steps): {serialMs:F3} ms serial, " +
                              $"{parallelMs:F3} ms effective in parallel.");
            Console.WriteLine($"A run of {evals} evaluations would take about {evals * serialMs / 1000:F0} s serial, " +
                              $"{evals * parallelMs / 1000:F0} s parallel.");
            return 0;
        }
        finally
        {
            (sim as IDisposable)?.Dispose();
        }
    }

    private static double ArcLength(Func<double, (double X, double Y)> curve)
    {
        double len = 0;
        var prev = curve(0);
        for (int i = 1; i <= 4000; i++)
        {
            var p = curve(i / 4000.0);
            len += Geometry.Distance(prev.X, prev.Y, p.X, p.Y);
            prev = p;
        }
        return len;
    }

    private class Options
    {
        public int? Task;
        public string? Config;
        public string? Experiment;
        public string? OutRoot;
        public List<int>? Seeds;
        public List<string> Overrides = new();

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
                switch (args[i])
                {
                    case "--task": o.Task = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--config": o.Config = Next(); break;
                    case "--experiment": o.Experiment = Next(); break;
                    case "--out": o.OutRoot = Next(); break;
                    case "--seed": o.Seeds = new() { int.Parse(Next(), CultureInfo.InvariantCulture) }; break;
                    case "--seeds": o.Seeds = ParseSeeds(Next()); break;
                    case "--set": o.Overrides.Add(Next()); break;
                    case "--simulator": o.Overrides.Add($"simulator.kind={Next()}"); break;
                    default: throw new ArgumentException($"Unknown option '{args[i]}'.");
                }
            }
            return o;
        }

        private static List<int> ParseSeeds(string s)
        {
            if (s.Contains('-'))
            {
                string[] p = s.Split('-');
                int a = int.Parse(p[0], CultureInfo.InvariantCulture), b = int.Parse(p[1], CultureInfo.InvariantCulture);
                return Enumerable.Range(a, b - a + 1).ToList();
            }
            return s.Split(',').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToList();
        }
    }
}
