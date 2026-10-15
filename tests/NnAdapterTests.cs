using Phase5.Shared;
using Phase5.Simulation;
using Xunit;

namespace Phase5.Tests;

/// <summary>Phase 5 report §3, checked against the real Phase 4 network.</summary>
public sealed class NnAdapterTests : IDisposable
{
    private readonly NnSimulatorAdapter sim = new("models/best_model.dat", "models/normalization.json");

    public void Dispose() => sim.Dispose();

    [Fact]
    public void Step_AppliesTheLocalPredictionWithOurFrameMaths()
    {
        var prev = new Command(0.1, 0.3);
        var cur = new Command(0.4, 0.35);
        var loc = new Location(1.5, 2.0, 2.4);

        var (dx, dy, dTheta) = sim.PredictLocal(prev, cur);
        Location expected = Geometry.ApplyLocalDelta(loc, dx, dy, dTheta);
        Location actual = sim.Step(prev, cur, loc);

        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
        Assert.Equal(expected.Theta, actual.Theta, 9);
    }

    [Fact]
    public void Adapter_PassesThrottleAndSteeringInTheRightOrder()
    {
        // Hard left with forward throttle must move forward and turn left. If the fields were swapped the
        // network would see throttle 0.6 and steering 0.3 and the result would differ.
        var (dx, _, dTheta) = sim.PredictLocal(new Command(0.6, 0.32), new Command(0.6, 0.32));
        Assert.True(dx > 0.05, $"dx = {dx}");
        Assert.True(dTheta > 0.1, $"dTheta = {dTheta}");

        var (_, _, dThetaRight) = sim.PredictLocal(new Command(-0.6, 0.32), new Command(-0.6, 0.32));
        Assert.True(dThetaRight < -0.1, $"dTheta = {dThetaRight}");
    }

    [Fact]
    public void StepSequence_StartsFromRest()
    {
        var cmds = new List<Command> { new(0.3, 0.35), new(0.3, 0.35) };
        var start = new Location(1, 1, 0);
        List<Location> path = sim.StepSequence(cmds, start);

        Assert.Equal(3, path.Count);
        Assert.Equal(start, path[0]);
        Location first = sim.Step(Command.Rest, cmds[0], start);
        Assert.Equal(first.X, path[1].X, 9);
        Assert.Equal(first.Theta, path[1].Theta, 9);
    }

    [Fact]
    public void ConstantLeftSteering_DrivesALeftCircle()
    {
        List<Location> path = sim.StepSequence(Enumerable.Repeat(new Command(0.6, 0.325), 40).ToList(), new Location(0, 0, 0));
        double turned = 0;
        for (int i = 1; i < path.Count; i++) turned += Geometry.WrapAngle(path[i].Theta - path[i - 1].Theta);
        Assert.True(turned > 2 * Math.PI, $"turned {turned} rad");
    }
}
