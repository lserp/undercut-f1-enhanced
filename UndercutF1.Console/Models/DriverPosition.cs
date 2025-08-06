namespace UndercutF1.Console.Models;

/// <summary>
/// Represents a driver's complete position information for circular track display
/// </summary>
public record DriverPosition
{
    /// <summary>
    /// Driver's racing number (e.g., "44", "1", "16")
    /// </summary>
    public string DriverNumber { get; init; } = string.Empty;

    /// <summary>
    /// Driver's three-letter abbreviation (e.g., "HAM", "VER", "LEC")
    /// </summary>
    public string DriverTla { get; init; } = string.Empty;

    /// <summary>
    /// Team color in hex format (e.g., "#00D2BE" for Mercedes)
    /// </summary>
    public string TeamColor { get; init; } = "#FFFFFF";

    /// <summary>
    /// Driver's position on the circular track
    /// </summary>
    public CircularPosition Position { get; init; } = new();

    /// <summary>
    /// Current driver status (on track, in pit, retired, etc.)
    /// </summary>
    public DriverStatus Status { get; init; } = DriverStatus.OnTrack;

    /// <summary>
    /// Gap to race leader (e.g., "+1.234", "LAP 45", "2L")
    /// </summary>
    public string GapToLeader { get; init; } = string.Empty;

    /// <summary>
    /// Interval to driver ahead (e.g., "+0.567")
    /// </summary>
    public string IntervalAhead { get; init; } = string.Empty;
}

/// <summary>
/// Represents the current status of a driver
/// </summary>
public enum DriverStatus
{
    OnTrack,
    InPit,
    PitOut,
    Retired,
    OffTrack,
    Stopped
}