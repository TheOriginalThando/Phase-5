using Phase5.Shared;
using Phase5.Simulation;
using Xunit;

namespace Phase5.Tests;

public class GeometryTests
{
    [Fact]
    public void FrameConversion_MatchesTheBriefsExample()
    {
        // Brief, Phase 2: from (10, 10, 45°) the car moves to (20, 15, 0);
        // in its local frame that is Δx = 10.606, Δy = -3.536, Δθ = -0.7853.
        Location next = Geometry.ApplyLocalDelta(new Location(10, 10, 0.7853), 10.606, -3.536, -0.7853);
        Assert.Equal(20, next.X, 2);
        Assert.Equal(15, next.Y, 2);
        Assert.Equal(0, next.Theta, 3);
    }

    [Fact]
    public void ToFrame_IsTheInverseOfToGlobal()
    {
        var frame = new Location(1.5, 2.0, 2.5);
        var rel = new Location(0.7, -0.3, -2.9);
        Location back = Geometry.ToFrame(frame, Geometry.ToGlobal(frame, rel));
        Assert.Equal(rel.X, back.X, 9);
        Assert.Equal(rel.Y, back.Y, 9);
        Assert.Equal(rel.Theta, back.Theta, 9);
    }

    [Theory]
    [InlineData(3.1, 0.2, -2.9831853)]
    [InlineData(-3.1, -0.2, 2.9831853)]
    [InlineData(0.5, 0.25, 0.75)]
    [InlineData(Math.PI, 2 * Math.PI, Math.PI)]
    public void WrapAngle_StaysInMinusPiToPi(double theta, double delta, double expected)
    {
        double wrapped = Geometry.WrapAngle(theta + delta);
        Assert.InRange(wrapped, -Math.PI, Math.PI);
        Assert.Equal(expected, wrapped, 6);
    }

    [Fact]
    public void Stub_StepSequenceReturnsNPlusOneAndStartsFromRest()
    {
        var sim = new StubSimulator(0.75, 2.0, 0.6);
        var start = new Location(1, 2, 0.3);
        var cmds = new List<Command> { new(0.2, 0.3), new(0.2, 0.3), new(-0.4, 0.35) };

        List<Location> path = sim.StepSequence(cmds, start);

        Assert.Equal(cmds.Count + 1, path.Count);
        Assert.Equal(start, path[0]);
        Assert.Equal(sim.Step(Command.Rest, cmds[0], start), path[1]);
        Assert.Equal(sim.Step(cmds[0], cmds[1], path[1]), path[2]);
    }

    [Fact]
    public void Stub_PositiveSteeringTurnsLeft()
    {
        var sim = new StubSimulator(0.75, 2.0, 0.6);
        List<Location> path = sim.StepSequence(Enumerable.Repeat(new Command(0.3, 0.3), 5).ToList(), new Location(0, 0, 0));
        Assert.True(path[^1].Theta > 0);
        Assert.True(path[^1].Y > 0);
    }
}
