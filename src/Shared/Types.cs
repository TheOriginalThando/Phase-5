namespace Phase5.Shared;

/// <summary>One command sent to the car every 0.2 s. Note the order: steering first, throttle second.
/// The NN input and the solution CSV are throttle-first, so be explicit at every boundary.</summary>
public record Command(double Steering, double Throttle)
{
    /// <summary>The "previous command" for the first step: the car starts at rest.</summary>
    public static readonly Command Rest = new(0.0, 0.0);
}

/// <summary>Pose in the global frame: metres, and radians in [-π, π] measured counter-clockwise from +x.</summary>
public record Location(double X, double Y, double Theta);

public interface ISimulator
{
    Location Step(Command prev, Command cur, Location curLoc);

    /// <summary>Returns N+1 locations: the start location followed by one per command.
    /// The first step uses <see cref="Command.Rest"/> as the previous command.</summary>
    List<Location> StepSequence(List<Command> commands, Location curLoc);
}
