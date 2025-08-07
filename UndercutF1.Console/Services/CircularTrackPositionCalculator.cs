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
    /// Calculates track progress (0.0-1.0) based on race position and gaps
    /// This creates a realistic representation where faster drivers are ahead
    /// </summary>
    /// <param name="driver">Driver timing data</param>
    /// <returns>Progress through current lap (0.0 = start, 1.0 = complete)</returns>
    public double GetTrackProgress(TimingDataPoint.Driver driver)
    {
        if (!driver.Line.HasValue) return 0.5; // Default middle position
        
        var racePosition = driver.Line.Value;
        
        // Base the track position primarily on race position
        // Leader (P1) gets position near 0.9 (almost complete lap)
        // Last place gets position near 0.1 (start of lap)
        var baseProgress = CalculateProgressFromRacePosition(racePosition);
        
        // Add gap-based fine-tuning for more accuracy
        var gapAdjustment = CalculateGapBasedAdjustment(driver);
        
        // Combine both for realistic positioning
        var totalProgress = baseProgress + gapAdjustment;
        
        // Ensure we stay within bounds
        return Math.Clamp(totalProgress, 0.05, 0.95);
    }

    /// <summary>
    /// Calculates track position using simple leader-at-12-o'clock approach
    /// Leader is at 0°, following drivers positioned counter-clockwise based on gaps
    /// </summary>
    /// <param name="driver">Current driver</param>
    /// <param name="allDrivers">All drivers in the session</param>
    /// <returns>Angle in degrees (0° = 12 o'clock, increases counter-clockwise)</returns>
    public double GetSimpleTrackAngle(TimingDataPoint.Driver driver, IEnumerable<TimingDataPoint.Driver> allDrivers)
    {
        if (!driver.Line.HasValue) return 180.0; // Default to 6 o'clock if no position
        
        var racePosition = driver.Line.Value;
        
        // Leader is always at 12 o'clock (0 degrees)
        if (racePosition == 1) return 0.0;
        
        var driversByPosition = allDrivers
            .Where(d => d.Line.HasValue)
            .OrderBy(d => d.Line.Value)
            .ToList();
        
        if (!driversByPosition.Any()) return 180.0;
        
        var currentAngle = 0.0; // Start at 12 o'clock for leader
        
        // Calculate cumulative angles based on gaps to car ahead
        for (var i = 1; i < driversByPosition.Count; i++)
        {
            var currentDriver = driversByPosition[i];
            var driverAhead = driversByPosition[i - 1];
            
            // Calculate gap between consecutive drivers
            var gapToAhead = CalculateGapBetweenConsecutiveDrivers(currentDriver, driverAhead);
            
            // Convert gap to degrees (assume 3 seconds = ~30 degrees for good spacing)
            var angleIncrement = Math.Clamp(gapToAhead * 10.0, 5.0, 45.0); // 5-45 degrees per gap
            currentAngle += angleIncrement;
            
            if (currentDriver.Line == driver.Line)
            {
                return currentAngle % 360.0; // Ensure we stay within 0-360
            }
        }
        
        return currentAngle % 360.0;
    }

    /// <summary>
    /// Calculates the gap between two consecutive drivers in race order
    /// </summary>
    private double CalculateGapBetweenConsecutiveDrivers(TimingDataPoint.Driver driver, TimingDataPoint.Driver driverAhead)
    {
        var driverGap = ParseGapToSeconds(driver.GapToLeader);
        var aheadGap = ParseGapToSeconds(driverAhead.GapToLeader);
        
        if (driverGap == null || aheadGap == null) return 2.0; // Default 2 second gap
        
        var gapBetween = driverGap.Value - aheadGap.Value;
        return Math.Max(gapBetween, 0.1); // Minimum 0.1 second gap
    }

    /// <summary>
    /// Calculates base progress from race position
    /// </summary>
    private double CalculateProgressFromRacePosition(int racePosition)
    {
        // Distribute 20 drivers around the track based on position
        // P1 = ~90% around track, P20 = ~10% around track
        var maxPosition = 20.0;
        var minProgress = 0.1;  // 10% around track (start area)
        var maxProgress = 0.9;  // 90% around track (near finish)
        
        // Invert position so P1 gets highest progress
        var normalizedPosition = (maxPosition - racePosition + 1) / maxPosition;
        
        // Map to progress range
        return minProgress + (normalizedPosition * (maxProgress - minProgress));
    }

    /// <summary>
    /// Calculates fine adjustment based on gap to leader
    /// </summary>
    private double CalculateGapBasedAdjustment(TimingDataPoint.Driver driver)
    {
        var gapString = driver.GapToLeader;
        if (string.IsNullOrEmpty(gapString)) return 0.0;
        
        // Parse gap to leader
        var gapSeconds = ParseGapToSeconds(gapString);
        if (!gapSeconds.HasValue) return 0.0;
        
        // Convert gap to track position adjustment
        // Assume ~90 seconds for a full lap (typical F1 lap time)
        var typicalLapTime = 90.0;
        var gapAsLapFraction = gapSeconds.Value / typicalLapTime;
        
        // Limit adjustment to prevent unrealistic positions
        var maxAdjustment = 0.1; // Max 10% of track
        var adjustment = Math.Clamp(-gapAsLapFraction, -maxAdjustment, maxAdjustment);
        
        return adjustment;
    }

    /// <summary>
    /// Parses gap string to seconds
    /// </summary>
    public double? ParseGapToSeconds(string gapString)
    {
        if (string.IsNullOrEmpty(gapString)) return null;
        
        // Handle different gap formats
        if (gapString.StartsWith("LAP")) return 0.0; // Leader
        if (gapString.EndsWith("L")) return null; // Lapped drivers - ignore for now
        
        // Parse time gaps like "+1.234" or "1.234"
        var cleanGap = gapString.TrimStart('+');
        return double.TryParse(cleanGap, out var seconds) ? seconds : null;
        
        return null;
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
    public double CalculateRadialPosition(int driverLap, int leaderLap)
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