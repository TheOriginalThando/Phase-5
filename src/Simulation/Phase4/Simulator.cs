using System.Text.Json;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TSTemplate;

public class Command
{
    public double Throttle { get; set; }
    public double Steering { get; set; }

    public Command() { }

    public Command(double throttle, double steering)
    {
        Throttle = throttle;
        Steering = steering;
    }

    public override string ToString() =>
        $"(throttle {Throttle:F3}, steering {Steering:F3})";
}

public class Location
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Theta { get; set; }

    public Location() { }

    public Location(double x, double y, double theta)
    {
        X = x;
        Y = y;
        Theta = theta;
    }

    public override string ToString() =>
        $"x={X,8:F4}  y={Y,8:F4}  theta={Theta,8:F4}";
}

public class Simulator : IDisposable
{
    private readonly TorchSharp.Modules.Sequential model;

    private readonly double[] inputMeans;
    private readonly double[] inputStds;
    private readonly double[] outputMeans;
    private readonly double[] outputStds;

    public Simulator(
        string modelPath = "best_model.dat",
        string normalizationPath = "normalization.json")
    {
        modelPath = ResolvePath(modelPath);
        normalizationPath = ResolvePath(normalizationPath);

        NormalizationData norm =
            JsonSerializer.Deserialize<NormalizationData>(
                File.ReadAllText(normalizationPath))
            ?? throw new Exception("Could not read normalization.json.");

        if (norm.InputMeans.Length != 4 || norm.InputStandardDeviations.Length != 4 ||
            norm.OutputMeans.Length != 3 || norm.OutputStandardDeviations.Length != 3)
        {
            throw new Exception("normalization.json does not match a 4 input, 3 output network.");
        }

        inputMeans = norm.InputMeans;
        inputStds = norm.InputStandardDeviations;
        outputMeans = norm.OutputMeans;
        outputStds = norm.OutputStandardDeviations;

        model = Sequential(
            ("input_to_hidden1", Linear(4, 64)),
            ("relu1", ReLU()),
            ("hidden1_to_hidden2", Linear(64, 32)),
            ("relu2", ReLU()),
            ("hidden2_to_output", Linear(32, 3)));

        model.load(modelPath);
        model.eval();
    }

    public (double Dx, double Dy, double DTheta) PredictLocal(Command prev, Command cur)
    {
        double[] raw =
        {
            prev.Throttle, prev.Steering,
            cur.Throttle, cur.Steering
        };

        float[] input = new float[4];
        for (int i = 0; i < 4; i++)
            input[i] = (float)((raw[i] - inputMeans[i]) / inputStds[i]);

        using var scope = torch.NewDisposeScope();
        using (torch.no_grad())
        {
            Tensor x = torch.tensor(input, dtype: ScalarType.Float32).reshape(1, 4);
            Tensor y = model.forward(x);
            float[] o = y.data<float>().ToArray();

            return (
                o[0] * outputStds[0] + outputMeans[0],
                o[1] * outputStds[1] + outputMeans[1],
                o[2] * outputStds[2] + outputMeans[2]);
        }
    }

    public Location Step(Command prev, Command cur, Location curloc)
    {
        var (dx, dy, dTheta) = PredictLocal(prev, cur);

        double c = Math.Cos(curloc.Theta);
        double s = Math.Sin(curloc.Theta);

        return new Location(
            curloc.X + dx * c - dy * s,
            curloc.Y + dx * s + dy * c,
            WrapAngle(curloc.Theta + dTheta));
    }

    public List<Location> StepSequence(List<Command> commands, Location curloc)
    {
        if (commands.Count == 0) return new List<Location> { curloc };
        return StepSequence(commands, curloc, commands[0]);
    }

    public List<Location> StepSequence(List<Command> commands, Location curloc, Command initialPrev)
    {
        List<Location> path = new() { curloc };

        Command prev = initialPrev;
        Location loc = curloc;

        foreach (Command cur in commands)
        {
            loc = Step(prev, cur, loc);
            path.Add(loc);
            prev = cur;
        }

        return path;
    }

    public static double WrapAngle(double angle) =>
        Math.Atan2(Math.Sin(angle), Math.Cos(angle));

    public static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path) && File.Exists(path)) return path;

        string inOutput = Path.Combine(AppContext.BaseDirectory, path);
        if (File.Exists(inOutput)) return inOutput;

        string inCurrent = Path.Combine(Directory.GetCurrentDirectory(), path);
        if (File.Exists(inCurrent)) return inCurrent;

        throw new FileNotFoundException(
            $"Could not find {path}. Add it to the project and set Copy to Output Directory to 'Copy if newer'.");
    }

    public void Dispose() => model.Dispose();

    private class NormalizationData
    {
        public string[] InputNames { get; set; } = Array.Empty<string>();
        public string[] OutputNames { get; set; } = Array.Empty<string>();
        public double[] InputMeans { get; set; } = Array.Empty<double>();
        public double[] InputStandardDeviations { get; set; } = Array.Empty<double>();
        public double[] OutputMeans { get; set; } = Array.Empty<double>();
        public double[] OutputStandardDeviations { get; set; } = Array.Empty<double>();
    }
}