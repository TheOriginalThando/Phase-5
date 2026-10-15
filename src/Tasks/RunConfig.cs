using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Phase5.Tasks;

/// <summary>Everything a run needs. Loaded from configs/taskN.json (snake_case keys).
/// Every tuning number lives here, never in code (CLAUDE.md §0.3).</summary>
public class RunConfig
{
    /// <summary>1 = point target, 2 = sine wave, 3 = figure eight.</summary>
    public int Task { get; set; } = 1;
    public string Experiment { get; set; } = "baseline";
    public int Seed { get; set; } = 1;
    public bool Parallel { get; set; } = true;

    public SimulatorConfig Simulator { get; set; } = new();
    public CommandLimits Limits { get; set; } = new();
    public int HoldSteps { get; set; } = 2;

    public double StartX { get; set; } = 1.5;
    public double StartY { get; set; } = 2.0;
    public double StartTheta { get; set; } = 0.0;
    public TrackBounds Bounds { get; set; } = new();

    public double WaypointSpacing { get; set; } = 0.18;
    /// <summary>Car's real minimum turning radius (m), used by the feasibility check.</summary>
    public double MinTurnRadius { get; set; } = 0.5;
    public PointTargetConfig Target { get; set; } = new();
    public SineConfig Sine { get; set; } = new();
    public FigureEightConfig Eight { get; set; } = new();

    public FitnessConfig Fitness { get; set; } = new();
    public EaConfig Ea { get; set; } = new();
    public List<StageConfig> Stages { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Loads a config file, applying "dotted.key=value" overrides (e.g. "fitness.lambda_s=0").</summary>
    public static RunConfig Load(string path, IEnumerable<string>? overrides = null)
    {
        JsonNode root = JsonNode.Parse(File.ReadAllText(path),
            documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
            ?? throw new InvalidDataException($"{path} is empty.");

        foreach (string o in overrides ?? Enumerable.Empty<string>())
            ApplyOverride(root, o);

        RunConfig cfg = root.Deserialize<RunConfig>(JsonOptions)
            ?? throw new InvalidDataException($"Could not read {path}.");
        cfg.Validate();
        return cfg;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    private static void ApplyOverride(JsonNode root, string assignment)
    {
        int eq = assignment.IndexOf('=');
        if (eq <= 0) throw new ArgumentException($"Override '{assignment}' must look like key=value.");
        string[] keys = assignment[..eq].Split('.');
        string raw = assignment[(eq + 1)..];

        JsonNode node = root;
        // Sections missing from the file are created; misspelt keys are caught when deserialising.
        foreach (string k in keys[..^1])
            node = node[k] ??= new JsonObject();

        // Parse as JSON (numbers, booleans, arrays); fall back to a plain string.
        JsonNode? value;
        try { value = JsonNode.Parse(raw); }
        catch (JsonException) { value = JsonValue.Create(raw); }
        node[keys[^1]] = value;
    }

    public void Validate()
    {
        if (Task is < 1 or > 3) throw new InvalidDataException("task must be 1, 2 or 3.");
        if (HoldSteps < 1) throw new InvalidDataException("hold_steps must be at least 1.");
        if (Limits.ThrottleMin > Limits.ThrottleMax) throw new InvalidDataException("throttle_min > throttle_max.");
        if (Stages.Count == 0) throw new InvalidDataException("At least one stage is required.");
        foreach (StageConfig s in Stages)
        {
            if (s.Genes < 1 || s.Generations < 1) throw new InvalidDataException($"Stage '{s.Name}' needs genes and generations >= 1.");
            if (s.PathFraction is <= 0 or > 1) throw new InvalidDataException($"Stage '{s.Name}': path_fraction must be in (0, 1].");
        }
        for (int i = 1; i < Stages.Count; i++)
            if (Stages[i].Genes < Stages[i - 1].Genes)
                throw new InvalidDataException("Each stage must have at least as many genes as the one before it.");
        if (Ea.Elite >= Ea.Population) throw new InvalidDataException("elite must be smaller than population.");
        if (Ea.ExtendMode is not ("default" or "mirror")) throw new InvalidDataException("ea.extend_mode must be 'default' or 'mirror'.");
    }
}

public class SimulatorConfig
{
    /// <summary>"nn" (Phase 4 neural network) or "stub" (kinematic model, §3).</summary>
    public string Kind { get; set; } = "nn";
    public string ModelPath { get; set; } = "models/best_model.dat";
    public string NormalizationPath { get; set; } = "models/normalization.json";
    public double StubKv { get; set; } = 0.75;
    public double StubKappaMax { get; set; } = 2.0;
}

public class CommandLimits
{
    public double SteeringMax { get; set; } = 0.6;
    public double ThrottleMin { get; set; } = 0.25;
    public double ThrottleMax { get; set; } = 0.4;

    public double SteeringRange => 2 * SteeringMax;
    public double ThrottleRange => ThrottleMax - ThrottleMin;
    public double ThrottleMid => 0.5 * (ThrottleMin + ThrottleMax);
}

public class TrackBounds
{
    public double MinX { get; set; } = 0.1;
    public double MaxX { get; set; } = 4.4;
    public double MinY { get; set; } = 0.05;
    public double MaxY { get; set; } = 3.9;
    /// <summary>Waypoints must be at least this far inside the bounds (m).</summary>
    public double Margin { get; set; } = 0.25;

    public bool Contains(double x, double y, double margin = 0) =>
        x >= MinX + margin && x <= MaxX - margin && y >= MinY + margin && y <= MaxY - margin;
}

public class PointTargetConfig
{
    public double X { get; set; } = 1.2;
    public double Y { get; set; } = 0.6;
}

public class SineConfig
{
    public double Amplitude { get; set; } = 0.25;
    public double Wavelength { get; set; } = 2.0;
    public double Length { get; set; } = 2.0;
}

public class FigureEightConfig
{
    /// <summary>"gerono" (CLAUDE.md §5) or "bernoulli". See Curves.FigureEight.</summary>
    public string Shape { get; set; } = "gerono";
    public double A { get; set; } = 0.8;
    public double B { get; set; } = 0.8;
}

public class FitnessConfig
{
    public double WEnd { get; set; } = 2.0;
    public double LambdaS { get; set; } = 0.1;
    public double POob { get; set; } = 10.0;
    public double WHead { get; set; } = 0.0;
}

public class EaConfig
{
    public int Population { get; set; } = 100;
    public int TournamentSize { get; set; } = 3;
    public int Elite { get; set; } = 2;
    public double CrossoverRate { get; set; } = 0.9;
    public double MutationGeneRate { get; set; } = 0.1;
    /// <summary>Mutation std dev as a fraction of each command's range, decaying linearly over each stage.</summary>
    public double SigmaStart { get; set; } = 0.08;
    public double SigmaEnd { get; set; } = 0.015;
    public double InitTurnFraction { get; set; } = 0.2;
    public double InitTurnSteering { get; set; } = 0.2;
    /// <summary>Noise added at initialisation, as a fraction of each range.</summary>
    public double InitNoise { get; set; } = 0.03;
    /// <summary>Fraction of the previous stage's population used to seed the next stage.</summary>
    public double SeedFraction { get; set; } = 0.2;
    /// <summary>How seeds are lengthened for the next stage: "default" (CLAUDE.md §8) or "mirror". See Operators.Extend.</summary>
    public string ExtendMode { get; set; } = "default";
}

public class StageConfig
{
    public string Name { get; set; } = "full";
    public int Genes { get; set; } = 10;
    public int Generations { get; set; } = 300;
    /// <summary>How much of the task's curve this stage covers (0.5 = first lobe of the eight).</summary>
    public double PathFraction { get; set; } = 1.0;
}
