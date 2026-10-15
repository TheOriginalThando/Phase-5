using System.Globalization;
using Phase5.Output;
using Phase5.Shared;
using Phase5.Tasks;
using Xunit;

namespace Phase5.Tests;

public class OutputAndTaskTests
{
    [Fact]
    public void SolutionCsv_MatchesTheLecturersLayout()
    {
        var start = new Location(1.5, 2.0, Math.PI / 2);
        var commands = new List<Command> { new(0.5, 0.3), new(-0.25, 0.35), new(0.1, 0.4) };
        var path = new List<Location>
        {
            start,
            Geometry.ToGlobal(start, new Location(0.1, 0.0, 0.05)),
            Geometry.ToGlobal(start, new Location(0.25, 0.01, 0.1)),
            Geometry.ToGlobal(start, new Location(0.4, 0.05, 0.12)),
        };
        string file = Path.GetTempFileName();
        CultureInfo old = CultureInfo.CurrentCulture;
        try
        {
            // A culture that uses a comma as the decimal separator must not leak into the file.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CsvWriters.WriteSolution(file, commands, path, start);
        }
        finally { CultureInfo.CurrentCulture = old; }

        byte[] bytes = File.ReadAllBytes(file);
        string text = File.ReadAllText(file);
        File.Delete(file);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "file has a BOM");
        Assert.EndsWith("\r\n", text);
        string[] rows = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(commands.Count, rows.Length); // one row per command, no header, no start row
        foreach (string row in rows)
        {
            string[] cols = row.Split(',');
            Assert.Equal(5, cols.Length);
            Assert.All(cols, c => double.Parse(c, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        // Row 1 is the pose after the first command, relative to the start pose, then throttle, then steering.
        Assert.Equal("0.1,0,0.05,0.3,0.5", rows[0]);
        Assert.Equal("0.4,0.05,0.12,0.4,0.1", rows[2]);
    }

    [Fact]
    public void FigureEight_StartsAlongPlusXAndReturnsToTheStart()
    {
        foreach (string shape in new[] { "gerono", "bernoulli" })
        {
            var curve = Curves.FigureEight(new FigureEightConfig { Shape = shape, A = 1.0, B = 0.8 });
            Assert.Equal(0, Curves.TangentAngle(curve, 0), 3);
            var end = curve(1.0);
            var crossing = curve(0.5);
            Assert.Equal(0, end.X, 6);
            Assert.Equal(0, end.Y, 6);
            Assert.Equal(0, crossing.X, 6); // the first lobe ends back at the crossing point
            Assert.Equal(0, crossing.Y, 6);
        }
    }

    [Fact]
    public void Waypoints_AreSpacedAlongTheCurveAndEndAtTheEnd()
    {
        var sine = new SineConfig { Amplitude = 0.25, Wavelength = 2, Length = 2 };
        var pts = Curves.Resample(Curves.Sine(sine), 1.0, 0.18);
        Assert.Equal((2.0, 0.0), (Math.Round(pts[^1].X, 9), Math.Round(pts[^1].Y, 9)));
        for (int i = 1; i < pts.Count - 1; i++)
        {
            double d = Geometry.Distance(pts[i].X, pts[i].Y, pts[i - 1].X, pts[i - 1].Y);
            Assert.InRange(d, 0.15, 0.19);
        }
    }

    [Fact]
    public void TaskDefinition_ThrowsWhenAWaypointLeavesTheTrack()
    {
        RunConfig cfg = EvolutionTests.StubConfig(task: 2);
        cfg.StartX = 3.5; // a 2 m sine from here runs off the 4.4 m track
        Assert.Throws<InvalidDataException>(() => TaskDefinition.Create(cfg));
    }

    [Fact]
    public void TaskDefinition_WarnsWhenTheSineIsTooTight()
    {
        RunConfig cfg = EvolutionTests.StubConfig(task: 2);
        cfg.MinTurnRadius = 0.5; // the default sine's sharpest radius is about 0.41 m
        Assert.Contains(TaskDefinition.Create(cfg).Warnings, w => w.Contains("too tight"));
    }

    [Fact]
    public void ConfigOverrides_ReachNestedValues()
    {
        string file = Path.GetTempFileName();
        File.WriteAllText(file, """{ "task": 1, "fitness": { "lambda_s": 0.1 }, "stages": [ { "genes": 6, "generations": 10 } ] }""");
        try
        {
            RunConfig cfg = RunConfig.Load(file, new[] { "fitness.lambda_s=0", "simulator.kind=stub" });
            Assert.Equal(0, cfg.Fitness.LambdaS);
            Assert.Equal("stub", cfg.Simulator.Kind);
        }
        finally { File.Delete(file); }
    }
}
