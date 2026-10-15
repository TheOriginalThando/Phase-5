using Phase5.Shared;
using Phase5.Tasks;

namespace Phase5.Evolution;

/// <summary>Initialisation, selection, crossover and mutation (CLAUDE.md §7). All randomness comes from
/// the single seeded <see cref="Random"/> passed in, so runs are reproducible.</summary>
public class Operators
{
    private readonly Random rng;
    private readonly CommandLimits limits;
    private readonly EaConfig ea;

    public Operators(Random rng, CommandLimits limits, EaConfig ea)
    {
        this.rng = rng;
        this.limits = limits;
        this.ea = ea;
    }

    /// <summary>Standard normal sample (Box–Muller).</summary>
    public double Gaussian()
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>"Straight at mid throttle" plus small noise.</summary>
    public Command DefaultGene() => Noisy(0.0, limits.ThrottleMid);

    private Command Noisy(double steering, double throttle) => Chromosome.Clip(new Command(
        steering + ea.InitNoise * limits.SteeringRange * Gaussian(),
        throttle + ea.InitNoise * limits.ThrottleRange * Gaussian()), limits);

    /// <summary>Initial population: most individuals drive straight at mid throttle, the rest
    /// (init_turn_fraction) hold a gentle constant turn. Both get small noise.</summary>
    public List<Chromosome> InitialPopulation(int size, int genes)
    {
        int turners = (int)Math.Round(size * ea.InitTurnFraction);
        var pop = new List<Chromosome>(size);
        for (int i = 0; i < size; i++)
        {
            double steer = i < size - turners ? 0.0 : (2 * rng.NextDouble() - 1) * ea.InitTurnSteering;
            var g = new Command[genes];
            for (int k = 0; k < genes; k++) g[k] = Noisy(steer, limits.ThrottleMid);
            pop.Add(new Chromosome(g));
        }
        return pop;
    }

    /// <summary>Copies the chromosome and appends genes until it has <paramref name="genes"/> genes (config ea.extend_mode):
    /// "default" appends default genes (CLAUDE.md §8); "mirror" repeats the existing genes with steering negated
    /// (plus init noise), which for a figure eight turns a first lobe into a mirrored second lobe.</summary>
    public Chromosome Extend(Chromosome c, int genes)
    {
        var g = new Command[genes];
        Array.Copy(c.Genes, g, Math.Min(c.Length, genes));
        for (int k = c.Length; k < genes; k++)
        {
            if (ea.ExtendMode == "mirror")
            {
                Command src = c.Genes[(k - c.Length) % c.Length];
                g[k] = Noisy(-src.Steering, src.Throttle);
            }
            else
            {
                g[k] = DefaultGene();
            }
        }
        return new Chromosome(g);
    }

    public Chromosome Tournament(IReadOnlyList<Chromosome> pop)
    {
        Chromosome best = pop[rng.Next(pop.Count)];
        for (int i = 1; i < ea.TournamentSize; i++)
        {
            Chromosome c = pop[rng.Next(pop.Count)];
            if (c.Fitness!.Total < best.Fitness!.Total) best = c;
        }
        return best;
    }

    /// <summary>Two-point crossover: the child is parent A with the genes between two cut points taken
    /// from parent B. Happens with probability crossover_rate, otherwise the child is a copy of A.</summary>
    public Chromosome Crossover(Chromosome a, Chromosome b)
    {
        var g = (Command[])a.Genes.Clone();
        if (rng.NextDouble() < ea.CrossoverRate && g.Length > 1)
        {
            int i = rng.Next(g.Length + 1), j = rng.Next(g.Length + 1);
            if (i > j) (i, j) = (j, i);
            Array.Copy(b.Genes, i, g, i, j - i);
        }
        return new Chromosome(g);
    }

    /// <summary>Gaussian mutation in place: each gene mutates with probability mutation_gene_rate, getting noise
    /// in both steering and throttle with std dev <paramref name="sigma"/> × that command's allowed range.</summary>
    public void Mutate(Chromosome c, double sigma)
    {
        for (int k = 0; k < c.Length; k++)
        {
            if (rng.NextDouble() >= ea.MutationGeneRate) continue;
            Command g = c.Genes[k];
            c.Genes[k] = Chromosome.Clip(new Command(
                g.Steering + sigma * limits.SteeringRange * Gaussian(),
                g.Throttle + sigma * limits.ThrottleRange * Gaussian()), limits);
        }
        c.Fitness = null;
    }
}
