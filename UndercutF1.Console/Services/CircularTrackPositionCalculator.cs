using UndercutF1.Console.Models;
using UndercutF1.Data;

namespace UndercutF1.Console.Services;

/// <summary>
/// Calculates driver positions for the circular track visualization
/// </summary>
public class CircularTrackPositionCalculator
{
    /// <summary>
    /// Calculates a driver's position on the circular track
    /// </summary>
    /// <param name="driver">Driver timing data</param>
    /// <param name="currentLap">Current race lap</param>
    /// <param name="leaderLap">Leader's current lap for multi-lap calculations</param>
    /// <returns>Circular position for the driver</returns>
    public CircularPosition CalculatePosition(TimingDataPoint.Driver driver, int currentLap, int leaderLap = 0)
    {
        var trackProgress = GetTrackProgress(driver);
        var lapNumber = driver.NumberOfLaps ?? currentLap;
        var angle = ConvertProgressToAngle(trackProgress);
        var radialPosition = CalculateRadialPosition(lapNumber, leaderLap);

        return new CircularPosition
        {
            Angle = angle,
            RadialPosition = radialPosition,
            LapNumber = lapNumber,
            TrackProgress = trackProgress
        };
    }

    /// <summary>
    /// Calculates track progress (0.0-1.0) based on sector completion
    /// </summary>
    /// <param name="driver">Driver timing data</param>
    /// <returns>Progress through current lap (0.0 = start, 1.0 = complete)</returns>
    public double GetTrackProgress(TimingDataPoint.Driver driver)
    {
        if (driver.Sectors == null || driver.Sectors.Count == 0)
        {
            // No sector data available, use basic position estimate
            return 0.05; // Assume just started lap
        }

        // Check sector completion (sectors are keyed as "0", "1", "2")
        var sector1Complete = !string.IsNullOrWhiteSpace(driver.Sectors.GetValueOrDefault("0")?.Value);
        var sector2Complete = !string.IsNullOrWhiteSpace(driver.Sectors.GetValueOrDefault("1")?.Value);
        var sector3Complete = !string.IsNullOrWhiteSpace(driver.Sectors.GetValueOrDefault("2")?.Value);

        // Use segment data for more precise positioning if available
        var segmentProgress = CalculateSegmentProgress(driver.Sectors);

        return (sector1Complete, sector2Complete, sector3Complete) switch
        {
            (false, false, false) => Math.Max(0.05, segmentProgress), // Just started lap
            (true, false, false) => Math.Max(0.33, segmentProgress),   // 1/3 through lap
            (true, true, false) => Math.Max(0.66, segmentProgress),    // 2/3 through lap
            (true, true, true) => Math.Max(0.95, segmentProgress),     // Almost complete
            _ => 0.05
        };
    }

    /// <summary>
    /// Calculates more precise progress using segment data within sectors
    /// </summary>
    private double CalculateSegmentProgress(Dictionary<string, TimingDataPoint.Driver.LapSectorTime> sectors)
    {
        // Look for segment completion in current sector
        foreach (var sector in sectors.Values)
        {
            if (sector.Segments != null)
            {
                var completedSegments = sector.Segments.Values
                    .Count(s => s.Status?.HasFlag(TimingDataPoint.Driver.StatusFlags.SegmentComplete) == true);
                
                if (completedSegments > 0)
                {
                    // Each sector has multiple segments, use this for finer positioning
                    var segmentRatio = (double)completedSegments / sector.Segments.Count;
                    return segmentRatio * 0.1; // Small increment within sector
                }
            }
        }

        return 0.0;
    }

    /// <summary>
    /// Converts track progress (0.0-1.0) to angle in degrees (0-360)
    /// </summary>
    /// <param name="progress">Track progress from 0.0 to 1.0</param>
    /// <returns>Angle in degrees, with 0 at the top (start/finish line)</returns>
    private double ConvertProgressToAngle(double progress)
    {
        // Convert progress to angle, with 0 degrees at the top (12 o'clock position)
        // Progress 0.0 = 0°, Progress 0.25 = 90°, Progress 0.5 = 180°, etc.
        var angle = progress * 360.0;
        
        // Ensure angle is within 0-360 range
        while (angle >= 360.0) angle -= 360.0;
        while (angle < 0.0) angle += 360.0;
        
        return angle;
    }

    /// <summary>
    /// Calculates radial position for multi-lap scenarios
    /// </summary>
    /// <param name="driverLap">Driver's current lap</param>
    /// <param name="leaderLap">Leader's current lap</param>
    /// <returns>Radial position (1.0 = current lap, lower values = laps behind)</returns>
    private double CalculateRadialPosition(int driverLap, int leaderLap)
    {
        if (leaderLap <= 0) return 1.0; // No leader data, use default

        var lapDifference = leaderLap - driverLap;
        
        return lapDifference switch
        {
            0 => 1.0,           // Same lap as leader
            1 => 0.9,           // One lap behind
            2 => 0.8,           // Two laps behind
            3 => 0.7,           // Three laps behind
            _ when lapDifference > 3 => 0.6, // More than 3 laps behind
            _ => 1.1            // Ahead of leader (shouldn't happen normally)
        };
    }

    /// <summary>
    /// Determines driver status based on timing data
    /// </summary>
    /// <param name="driver">Driver timing data</param>
    /// <returns>Current driver status</returns>
    public DriverStatus GetDriverStatus(TimingDataPoint.Driver driver)
    {
        if (driver.Retired == true) return DriverStatus.Retired;
        if (driver.Stopped == true) return DriverStatus.Stopped;
        if (driver.InPit == true) return DriverStatus.InPit;
        if (driver.PitOut == true) return DriverStatus.PitOut;
        
        // Check if driver is off track based on status flags
        return driver.Status?.HasFlag(TimingDataPoint.Driver.StatusFlags.PitLane) == true 
            ? DriverStatus.InPit 
            : DriverStatus.OnTrack;
    }
}