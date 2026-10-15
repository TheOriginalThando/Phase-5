using Phase5.Shared;
using Phase5.Tasks;

namespace Phase5.Simulation;

public static class SimulatorFactory
{
    /// <summary>The one place that decides which simulator the EA gets (config: simulator.kind).</summary>
    public static ISimulator Create(RunConfig cfg) => cfg.Simulator.Kind.ToLowerInvariant() switch
    {
        "nn" => new NnSimulatorAdapter(cfg.Simulator.ModelPath, cfg.Simulator.NormalizationPath),
        "stub" => new StubSimulator(cfg.Simulator.StubKv, cfg.Simulator.StubKappaMax, cfg.Limits.SteeringMax),
        _ => throw new InvalidDataException($"Unknown simulator kind '{cfg.Simulator.Kind}' (use 'nn' or 'stub')."),
    };
}
