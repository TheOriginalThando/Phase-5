using Phase5.Shared;
using P4 = TSTemplate;

namespace Phase5.Simulation;

/// <summary>Wraps the Phase 4 simulator (src/Simulation/Phase4/Simulator.cs, copied unchanged) behind
/// <see cref="ISimulator"/>. Phase 4's Command is (throttle, steering); ours is (steering, throttle).</summary>
public sealed class NnSimulatorAdapter : ISimulator, IDisposable
{
    private readonly P4.Simulator inner;

    public NnSimulatorAdapter(string modelPath, string normalizationPath)
    {
        inner = new P4.Simulator(modelPath, normalizationPath);
    }

    public Location Step(Command prev, Command cur, Location curLoc) =>
        FromP4(inner.Step(ToP4(prev), ToP4(cur), ToP4(curLoc)));

    public List<Location> StepSequence(List<Command> commands, Location curLoc)
    {
        // Phase 4's two-argument overload reuses commands[0] as the first "previous" command.
        // We start from rest instead (CLAUDE.md §2), so pass (0, 0) explicitly.
        List<P4.Location> path = inner.StepSequence(commands.Select(ToP4).ToList(), ToP4(curLoc), ToP4(Command.Rest));
        return path.Select(FromP4).ToList();
    }

    /// <summary>Raw NN prediction in the car's local frame, for tests and the measure command.</summary>
    public (double Dx, double Dy, double DTheta) PredictLocal(Command prev, Command cur) =>
        inner.PredictLocal(ToP4(prev), ToP4(cur));

    public void Dispose() => inner.Dispose();

    private static P4.Command ToP4(Command c) => new(throttle: c.Throttle, steering: c.Steering);
    private static P4.Location ToP4(Location l) => new(l.X, l.Y, l.Theta);
    private static Location FromP4(P4.Location l) => new(l.X, l.Y, l.Theta);
}
