using Phase5.Shared;

namespace Phase5.Simulation;

/// <summary>Simple kinematic stand-in for the NN (CLAUDE.md §3). Not for serious tuning.</summary>
public class StubSimulator : ISimulator
{
    private readonly double kv;
    private readonly double kappaMax;
    private readonly double steeringMax;

    public StubSimulator(double kv, double kappaMax, double steeringMax)
    {
        this.kv = kv;
        this.kappaMax = kappaMax;
        this.steeringMax = steeringMax;
    }

    public Location Step(Command prev, Command cur, Location curLoc)
    {
        double sigmaEff = 0.5 * (prev.Steering + cur.Steering);
        double tauEff = 0.5 * (prev.Throttle + cur.Throttle);
        double d = kv * tauEff;
        double dTheta = d * kappaMax * (sigmaEff / steeringMax);
        return Geometry.ApplyLocalDelta(curLoc, d * Math.Cos(dTheta / 2), d * Math.Sin(dTheta / 2), dTheta);
    }

    public List<Location> StepSequence(List<Command> commands, Location curLoc)
    {
        var path = new List<Location>(commands.Count + 1) { curLoc };
        Command prev = Command.Rest;
        foreach (Command cur in commands)
        {
            curLoc = Step(prev, cur, curLoc);
            path.Add(curLoc);
            prev = cur;
        }
        return path;
    }
}
