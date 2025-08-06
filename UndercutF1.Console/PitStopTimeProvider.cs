using UndercutF1.Data;

namespace UndercutF1.Console;

public class PitStopTimeProvider
{
    // Track-specific pit stop time losses (in seconds)
    // Based on historical F1 data and analysis
    private static readonly Dictionary<string, PitStopTiming> TrackTimings = new()
    {
        // Short pit lanes
        ["Monaco"] = new(22.5, 2.5, 8.0, 12.0),
        ["Zandvoort"] = new(19.0, 2.3, 7.5, 9.2),
        ["Hungaroring"] = new(20.0, 2.4, 8.0, 9.6),
        ["Austria"] = new(21.0, 2.3, 8.2, 10.5),
        
        // Medium pit lanes
        ["Silverstone"] = new(24.0, 2.8, 9.2, 12.0),
        ["Imola"] = new(23.0, 2.6, 8.8, 11.6),
        ["Barcelona"] = new(24.0, 2.7, 9.0, 12.3),
        ["Suzuka"] = new(25.0, 2.8, 9.5, 12.7),
        ["Miami"] = new(23.5, 2.6, 8.9, 12.0),
        ["Las Vegas"] = new(24.5, 2.7, 9.3, 12.5),
        
        // Long pit lanes
        ["Spa-Francorchamps"] = new(29.0, 3.0, 11.0, 15.0),
        ["Monza"] = new(27.0, 2.8, 10.2, 14.0),
        ["Baku"] = new(27.5, 2.9, 10.5, 14.1),
        ["Jeddah"] = new(25.5, 2.7, 9.8, 13.0),
        ["Abu Dhabi"] = new(26.0, 2.8, 10.0, 13.2),
        ["Interlagos"] = new(25.5, 2.7, 9.8, 13.0),
        ["Mexico City"] = new(26.5, 2.8, 10.2, 13.5),
        ["Singapore"] = new(24.5, 2.6, 9.4, 12.5),
        ["Qatar"] = new(25.0, 2.7, 9.6, 12.7),
    };

    // Default timing for unknown tracks
    private static readonly PitStopTiming DefaultTiming = new(24.0, 2.7, 9.0, 12.3);

    public record PitStopTiming(
        double TotalTimeLoss,    // Total time lost vs staying on track
        double StopTime,         // Actual tire change time  
        double PitLaneTime,      // Time in pit lane at speed limit
        double TransitionTime    // Deceleration + acceleration phases
    );

    public PitStopTiming GetTimingForTrack(string? trackName)
    {
        if (string.IsNullOrEmpty(trackName))
            return DefaultTiming;

        // Try to match track name (case insensitive, partial matching)
        var matchingTrack = TrackTimings.Keys
            .FirstOrDefault(key => 
                trackName.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                key.Contains(trackName, StringComparison.OrdinalIgnoreCase));

        return matchingTrack != null ? TrackTimings[matchingTrack] : DefaultTiming;
    }

    public double GetTotalTimeLoss(string? trackName, PitStopSeriesProcessor? pitStopData = null, string? driverNumber = null)
    {
        // If we have live pit stop data, use it for more accuracy
        if (pitStopData?.Latest.PitTimes.TryGetValue(driverNumber ?? "", out var driverPitTimes) == true)
        {
            var latestPitStop = driverPitTimes.Values
                .Where(pt => pt.PitStop != null)
                .OrderByDescending(pt => pt.Timestamp)
                .FirstOrDefault()?.PitStop;

            if (latestPitStop != null && 
                double.TryParse(latestPitStop.PitLaneTime, out var pitLaneTime) &&
                double.TryParse(latestPitStop.PitStopTime, out var pitStopTime))
            {
                // Add estimated transition time (decel/accel) to the pit lane time
                var trackTiming = GetTimingForTrack(trackName);
                return pitLaneTime + trackTiming.TransitionTime;
            }
        }

        // Fall back to track-specific default
        return GetTimingForTrack(trackName).TotalTimeLoss;
    }
}