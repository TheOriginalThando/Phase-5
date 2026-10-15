using Phase5.Fitness;
using Phase5.Shared;
using Phase5.Tasks;

namespace Phase5.Evolution;

/// <summary>A list of genes; each gene is one command held for hold_steps timesteps (Phase 5 report §4.1).</summary>
public class Chromosome
{
    public Command[] Genes { get; }
    public FitnessResult? Fitness { get; set; }

    public Chromosome(Command[] genes) => Genes = genes;

    public int Length => Genes.Length;

    public Chromosome Clone() => new((Command[])Genes.Clone()) { Fitness = Fitness };

    /// <summary>Repeats each gene holdSteps times: gene i covers timesteps holdSteps·i .. holdSteps·i + holdSteps − 1.</summary>
    public static List<Command> Expand(IReadOnlyList<Command> genes, int holdSteps)
    {
        var commands = new List<Command>(genes.Count * holdSteps);
        foreach (Command g in genes)
            for (int k = 0; k < holdSteps; k++)
                commands.Add(g);
        return commands;
    }

    public List<Command> Expand(int holdSteps) => Expand(Genes, holdSteps);

    public static Command Clip(Command c, CommandLimits limits) => new(
        Math.Clamp(c.Steering, -limits.SteeringMax, limits.SteeringMax),
        Math.Clamp(c.Throttle, limits.ThrottleMin, limits.ThrottleMax));
}
