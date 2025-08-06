namespace UndercutF1.Console.Models;

/// <summary>
/// Represents a driver's position on the circular track visualization
/// </summary>
public record CircularPosition
{
    /// <summary>
    /// Angle in degrees around the circle (0-360)
    /// 0 degrees represents the start/finish line at the top of the circle
    /// </summary>
    public double Angle { get; init; }

    /// <summary>
    /// Distance from center for multi-lap scenarios
    /// 1.0 = current lap, 0.9 = one lap behind, etc.
    /// </summary>
    public double RadialPosition { get; init; } = 1.0;

    /// <summary>
    /// Current lap number for the driver
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Progress through current lap (0.0 = start, 1.0 = complete)
    /// </summary>
    public double TrackProgress { get; init; }
}