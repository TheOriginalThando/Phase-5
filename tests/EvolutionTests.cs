using Phase5.Evolution;
using Phase5.Fitness;
using Phase5.Shared;
using Phase5.Simulation;
using Phase5.Tasks;
using Xunit;

namespace Phase5.Tests;

public class EvolutionTests
{
    private static readonly CommandLimits Limits = new() { SteeringMax = 0.6, ThrottleMin = 0.25, ThrottleMax = 0.4 };

    [Fact]
    public void GeneExpansion_RepeatsEachGeneHoldStepsTimesInOrder()
    {
        var genes = new[] { new Command(0.1, 0.3), new Command(-0.2, 0.35), new Command(0.5, 0.4) };
        List<Command> cmds = Chromosome.Expand(genes, 2);

        Assert.Equal(6, cmds.Count);
        Assert.Equal(new[] { genes[0], genes[0], genes[1], genes[1], genes[2], genes[2] }, cmds);
        Assert.Equal(9, Chromosome.Expand(genes, 3).Count);
    }

    [Fact]
    public void Mutation_NeverLeavesTheLimits()
    {
        var ea = new EaConfig { MutationGeneRate = 1.0, InitNoise = 0.5, InitTurnFraction = 0.5, InitTurnSteering = 0.6 };
        var ops = new Operators(new Random(7), Limits, ea);

        foreach (Chromosome c in ops.InitialPopulation(50, 30))
        {
            for (int round = 0; round < 20; round++) ops.Mutate(c, sigma: 2.0); // huge noise
            foreach (Command g in c.Genes)
            {
                Assert.InRange(g.Steering, -Limits.SteeringMax, Limits.SteeringMax);
                Assert.InRange(g.Throttle, Limits.ThrottleMin, Limits.ThrottleMax);
            }
        }
    }

    [Fact]
    public void Crossover_KeepsLengthAndOnlyUsesParentGenes()
    {
        var ops = new Operators(new Random(3), Limits, new EaConfig { CrossoverRate = 1.0 });
        var a = new Chromosome(Enumerable.Range(0, 12).Select(_ => new Command(0.1, 0.3)).ToArray());
        var b = new Chromosome(Enumerable.Range(0, 12).Select(_ => new Command(-0.1, 0.4)).ToArray());
        for (int i = 0; i < 50; i++)
        {
            Chromosome child = ops.Crossover(a, b);
            Assert.Equal(12, child.Length);
            Assert.All(child.Genes, g => Assert.True(g == a.Genes[0] || g == b.Genes[0]));
        }
    }

    [Fact]
    public void OrderedWaypointError_PenalisesVisitingOutOfOrder()
    {
        var wps = new List<Waypoint> { new(1, 0), new(2, 0), new(3, 0) };
        List<Location> inOrder = new[] { 0.0, 1, 2, 3 }.Select(x => new Location(x, 0, 0)).ToList();
        // Same points, but visits waypoint 3 before 1 and 2.
        List<Location> outOfOrder = new[] { 0.0, 3, 2, 1 }.Select(x => new Location(x, 0, 0)).ToList();

        double good = FitnessFunction.OrderedWaypointError(inOrder, wps);
        double bad = FitnessFunction.OrderedWaypointError(outOfOrder, wps);

        Assert.Equal(0, good, 9);
        Assert.True(bad > good + 0.5, $"out of order scored {bad}");
    }

    [Fact]
    public void Smoothness_IsZeroForConstantCommandsAndMatchesTheFormula()
    {
        Assert.Equal(0, FitnessFunction.Smoothness(Enumerable.Repeat(new Command(0.2, 0.3), 5).ToList(), Limits));
        var genes = new List<Command> { new(-0.6, 0.25), new(0.6, 0.25), new(0.6, 0.35) };
        // (1.2/1.2 + 0) and (0 + 0.1/0.4), averaged over 2 transitions.
        Assert.Equal((1.0 + 0.25) / 2, FitnessFunction.Smoothness(genes, Limits), 9);
    }

    [Fact]
    public void Run_IsReproducibleAndParallelMatchesSerial()
    {
        RunConfig cfg = StubConfig(task: 1);
        double Final(bool parallel)
        {
            cfg.Parallel = parallel;
            TaskDefinition task = TaskDefinition.Create(cfg);
            var engine = new EvolutionEngine(cfg, SimulatorFactory.Create(cfg), task,
                new FitnessFunction(cfg.Fitness, cfg.Limits, cfg.Bounds));
            return engine.Run().Best.Fitness!.Total;
        }

        double serial = Final(false);
        Assert.Equal(serial, Final(false));
        Assert.Equal(serial, Final(true));
    }

    [Fact]
    public void StagedRun_BestFitnessNeverGetsWorseWithinAStage()
    {
        RunConfig cfg = StubConfig(task: 3);
        cfg.Stages = new()
        {
            new StageConfig { Name = "lobe1", Genes = 10, Generations = 15, PathFraction = 0.5 },
            new StageConfig { Name = "full", Genes = 20, Generations = 15, PathFraction = 1.0 },
        };
        TaskDefinition task = TaskDefinition.Create(cfg);
        var engine = new EvolutionEngine(cfg, SimulatorFactory.Create(cfg), task,
            new FitnessFunction(cfg.Fitness, cfg.Limits, cfg.Bounds));

        var log = new List<GenerationStats>();
        EvolutionResult result = engine.Run(log.Add);

        Assert.Equal(30, log.Count);
        Assert.Equal(20, result.Best.Length);
        Assert.Equal(40, result.Commands.Count);
        foreach (var stage in log.GroupBy(s => s.Stage))
        {
            double[] best = stage.Select(s => s.Best).ToArray();
            for (int i = 1; i < best.Length; i++) Assert.True(best[i] <= best[i - 1] + 1e-12);
        }
    }

    internal static RunConfig StubConfig(int task)
    {
        var cfg = new RunConfig
        {
            Task = task,
            Seed = 42,
            Simulator = new SimulatorConfig { Kind = "stub" },
            Limits = Limits,
            Ea = new EaConfig { Population = 30 },
            MinTurnRadius = 0.1,
            Stages = new() { new StageConfig { Genes = 8, Generations = 20 } },
        };
        cfg.Eight.Shape = "bernoulli";
        cfg.Eight.A = 1.0;
        return cfg;
    }
}
