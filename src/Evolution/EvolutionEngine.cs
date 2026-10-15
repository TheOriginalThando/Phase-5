using System.Diagnostics;
using Phase5.Fitness;
using Phase5.Shared;
using Phase5.Tasks;

namespace Phase5.Evolution;

public record GenerationStats(int Generation, int Stage, string StageName, double Best, double Mean, double Std, FitnessResult BestTerms);

public record EvolutionResult(
    Chromosome Best,
    List<Command> Commands,
    List<Location> Path,
    long Evaluations,
    TimeSpan Elapsed);

/// <summary>Generational EA with tournament selection, elitism, two-point crossover and Gaussian mutation,
/// run over a list of stages (Phase 5 report §4.4–4.5). A single stage is ordinary evolution from scratch.</summary>
public class EvolutionEngine
{
    private readonly RunConfig cfg;
    private readonly ISimulator sim;
    private readonly TaskDefinition task;
    private readonly FitnessFunction fitness;
    private readonly Operators ops;
    private long evaluations;

    public EvolutionEngine(RunConfig cfg, ISimulator sim, TaskDefinition task, FitnessFunction fitness)
    {
        this.cfg = cfg;
        this.sim = sim;
        this.task = task;
        this.fitness = fitness;
        ops = new Operators(new Random(cfg.Seed), cfg.Limits, cfg.Ea);
    }

    public EvolutionResult Run(Action<GenerationStats>? onGeneration = null)
    {
        var timer = Stopwatch.StartNew();
        List<Chromosome> pop = new();
        int generation = 0;

        for (int s = 0; s < task.Stages.Count; s++)
        {
            StagePlan stage = task.Stages[s];
            pop = s == 0
                ? ops.InitialPopulation(cfg.Ea.Population, stage.Genes)
                : SeedFromPreviousStage(pop, stage.Genes);

            for (int g = 0; g < stage.Generations; g++, generation++)
            {
                Evaluate(pop, stage);
                pop.Sort((a, b) => a.Fitness!.Total.CompareTo(b.Fitness!.Total));
                onGeneration?.Invoke(Stats(pop, generation, s, stage));

                if (g < stage.Generations - 1)
                    pop = NextGeneration(pop, Sigma(g, stage.Generations));
            }
        }

        Chromosome best = pop[0];
        List<Command> commands = best.Expand(cfg.HoldSteps);
        return new EvolutionResult(best, commands, sim.StepSequence(commands, task.Start), evaluations, timer.Elapsed);
    }

    /// <summary>Mutation size decays linearly from sigma_start to sigma_end over each stage.</summary>
    private double Sigma(int g, int generations) =>
        generations <= 1 ? cfg.Ea.SigmaStart
            : cfg.Ea.SigmaStart + (cfg.Ea.SigmaEnd - cfg.Ea.SigmaStart) * g / (generations - 1);

    private List<Chromosome> NextGeneration(List<Chromosome> sorted, double sigma)
    {
        var next = new List<Chromosome>(sorted.Count);
        for (int i = 0; i < cfg.Ea.Elite; i++)
            next.Add(sorted[i].Clone());

        while (next.Count < sorted.Count)
        {
            Chromosome child = ops.Crossover(ops.Tournament(sorted), ops.Tournament(sorted));
            ops.Mutate(child, sigma);
            next.Add(child);
        }
        return next;
    }

    /// <summary>Top seed_fraction of the previous stage, extended with default genes, plus mutated copies of them.</summary>
    private List<Chromosome> SeedFromPreviousStage(List<Chromosome> sortedPrev, int genes)
    {
        int nSeeds = Math.Max(1, (int)Math.Ceiling(cfg.Ea.SeedFraction * sortedPrev.Count));
        List<Chromosome> seeds = sortedPrev.Take(nSeeds).Select(c => ops.Extend(c, genes)).ToList();

        var pop = new List<Chromosome>(cfg.Ea.Population);
        pop.AddRange(seeds);
        for (int i = 0; pop.Count < cfg.Ea.Population; i++)
        {
            Chromosome copy = seeds[i % seeds.Count].Clone();
            ops.Mutate(copy, cfg.Ea.SigmaStart);
            pop.Add(copy);
        }
        return pop;
    }

    private void Evaluate(List<Chromosome> pop, StagePlan stage)
    {
        void One(Chromosome c)
        {
            if (c.Fitness != null) return; // elites keep their fitness; evaluation is deterministic
            List<Location> path = sim.StepSequence(c.Expand(cfg.HoldSteps), task.Start);
            c.Fitness = fitness.Evaluate(c.Genes, path, stage);
            Interlocked.Increment(ref evaluations);
        }

        if (cfg.Parallel) System.Threading.Tasks.Parallel.ForEach(pop, One);
        else pop.ForEach(One);
    }

    private static GenerationStats Stats(List<Chromosome> sorted, int generation, int stage, StagePlan plan)
    {
        double mean = sorted.Average(c => c.Fitness!.Total);
        double var = sorted.Average(c => Math.Pow(c.Fitness!.Total - mean, 2));
        return new GenerationStats(generation, stage + 1, plan.Name, sorted[0].Fitness!.Total, mean, Math.Sqrt(var), sorted[0].Fitness!);
    }
}
