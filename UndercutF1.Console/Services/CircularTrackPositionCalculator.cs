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
    /// Calculates track progress (0.0-1.0) based on race position and sector completion
    /// </summary>
    /// <param name="driver">Driver timing data</param>
    /// <returns>Progress through current lap (0.0 = start, 1.0 = complete)</returns>
    public double GetTrackProgress(TimingDataPoint.Driver driver)
    {
        // Start with sector-based progress
        var sectorProgress = GetSectorBasedProgress(driver);
        
        // Add fine-grained positioning based on race position
        var positionOffset = GetPositionBasedOffset(driver);
        
        // Combine both for smoother positioning
        var combinedProgress = sectorProgress + positionOffset;
        
        // Ensure we stay within bounds
        return Math.Clamp(combinedProgress, 0.01, 0.99);
    }

    /// <summary>
    /// Gets basic progress based on sector completion
    /// </summary>
    private double GetSectorBasedProgress(TimingDataPoint.Driver driver)
    {
        if (driver.Sectors == null || driver.Sectors.Count == 0)
        {
            return 0.1; // Default start position
        }

        // Check sector completion (sectors are keyed as "0", "1", "2")
        var sector1Complete = !string.IsNullOrWhiteSpace(driver.Sectors.GetValueOrDefault("0")?.Value);
        var sector2Complete = !string.IsNullOrWhiteSpace(driver.Sectors.GetValueOrDefault("1")?.Value);
        var sector3Complete = !string.IsNullOrWhiteSpace(driver.Sectors.GetValueOrDefault("2")?.Value);

        return (sector1Complete, sector2Complete, sector3Complete) switch
        {
            (false, false, false) => 0.1,  // Start of lap
            (true, false, false) => 0.35,  // After sector 1
            (true, true, false) => 0.65,   // After sector 2
            (true, true, true) => 0.9,     // After sector 3
            _ => 0.1
        };
    }

    /// <summary>
    /// Gets fine-grained offset based on race position to spread out drivers
    /// </summary>
    private double GetPositionBasedOffset(TimingDataPoint.Driver driver)
    {
        if (!driver.Line.HasValue) return 0.0;
        
        // Use race position to create small offsets that spread drivers around the track
        // This prevents all drivers from clustering at the same sector positions
        var position = driver.Line.Value;
        
        // Create a small offset based on position (max 0.15 of track circumference)
        var maxOffset = 0.15;
        var positionFactor = (position - 1) % 20; // Cycle through positions 0-19
        var offset = (positionFactor / 20.0) * maxOffset;
        
        return offset;
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
        // Add some randomization to prevent all drivers from being at exact same positions
        var baseAngle = progress * 360.0;
        
        // Add a small random offset based on some driver characteristic to spread them out
        // This helps prevent clustering at sector boundaries
        var offset = (progress * 1000) % 10 - 5; // Small offset between -5 and +5 degrees
        
        var angle = baseAngle + offset;
        
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