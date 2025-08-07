using UndercutF1.Console.Models;
using UndercutF1.Console.Services;
using UndercutF1.Data;

namespace UndercutF1.Console.Api;

public static class CircularTrackEndpoints
{
    public static WebApplication MapCircularTrackEndpoints(this WebApplication app)
    {
        app.MapGet("/api/circular-track/positions", GetCircularTrackPositions)
            .WithTags("Circular Track")
            .WithSummary("Get current driver positions for circular track visualization");

        app.MapGet("/api/circular-track/session-info", GetSessionInfo)
            .WithTags("Circular Track")
            .WithSummary("Get session information for circular track display");

        app.MapGet("/api/circular-track/debug", GetDebugInfo)
            .WithTags("Circular Track")
            .WithSummary("Get debug information for position calculation");

        return app;
    }

    private static IResult GetCircularTrackPositions(
        TimingDataProcessor timingData,
        DriverListProcessor driverList,
        LapCountProcessor lapCount,
        CircularTrackPositionCalculator positionCalculator)
    {
        try
        {
            if (timingData?.Latest?.Lines == null || !timingData.Latest.Lines.Any())
            {
                return Results.Ok(new { drivers = Array.Empty<object>(), message = "No timing data available" });
            }

            var currentLap = lapCount?.Latest?.CurrentLap ?? 1;
            var leaderLap = GetLeaderLap(timingData, lapCount);
            var positions = new List<object>();

            // Get all drivers and sort by race position for consistent ordering
            var sortedDrivers = timingData.Latest.Lines
                .Where(kvp => kvp.Value.Line.HasValue)
                .OrderBy(kvp => kvp.Value.Line.Value)
                .ToList();

            foreach (var (driverNumber, timingLine) in sortedDrivers)
            {
                var driver = driverList?.Latest?.GetValueOrDefault(driverNumber);
                if (driver == null) continue;

                // Skip retired or knocked out drivers
                if (timingLine.Retired == true || timingLine.KnockedOut == true) continue;

                // Use simple leader-at-12-o'clock positioning
                var angle = positionCalculator.GetSimpleTrackAngle(timingLine, 
                    timingData.Latest.Lines.Values);
                var radialPosition = positionCalculator.CalculateRadialPosition(
                    timingLine.NumberOfLaps ?? currentLap, leaderLap);
                var trackProgress = angle / 360.0; // Convert angle back to progress for compatibility
                
                var circularPosition = new CircularPosition
                {
                    Angle = angle,
                    RadialPosition = radialPosition,
                    LapNumber = timingLine.NumberOfLaps ?? currentLap,
                    TrackProgress = trackProgress
                };
                
                var status = positionCalculator.GetDriverStatus(timingLine);

                var driverPosition = new
                {
                    driverNumber,
                    driverTla = driver.Tla ?? driverNumber,
                    driverName = driver.FullName ?? driver.Tla ?? driverNumber,
                    teamName = driver.TeamName ?? "Unknown Team",
                    teamColor = driver.TeamColour ?? "FFFFFF",
                    position = new
                    {
                        angle = circularPosition.Angle,
                        radialPosition = circularPosition.RadialPosition,
                        lapNumber = circularPosition.LapNumber,
                        trackProgress = circularPosition.TrackProgress
                    },
                    status = status.ToString(),
                    racePosition = timingLine.Line ?? 99,
                    gapToLeader = timingLine.GapToLeader ?? "",
                    intervalAhead = timingLine.IntervalToPositionAhead?.Value ?? "",
                    lastLapTime = timingLine.LastLapTime?.Value ?? "",
                    bestLapTime = timingLine.BestLapTime?.Value ?? "",
                    inPit = timingLine.InPit ?? false,
                    pitOut = timingLine.PitOut ?? false,
                    numberOfPitStops = timingLine.NumberOfPitStops ?? 0,
                    // Add debug info
                    debugInfo = new
                    {
                        rawGap = timingLine.GapToLeader,
                        calculatedProgress = circularPosition.TrackProgress,
                        calculatedAngle = circularPosition.Angle
                    }
                };

                positions.Add(driverPosition);
            }

            return Results.Ok(new { 
                drivers = positions,
                totalDrivers = positions.Count,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error calculating positions: {ex.Message}");
        }
    }

    private static IResult GetSessionInfo(
        SessionInfoProcessor sessionInfo,
        LapCountProcessor lapCount,
        TimingDataProcessor timingData)
    {
        try
        {
            var info = new
            {
                sessionName = sessionInfo?.Latest?.Name ?? "Unknown Session",
                sessionType = sessionInfo?.Latest?.Type ?? "Unknown",
                meetingName = sessionInfo?.Latest?.Meeting?.Name ?? "",
                currentLap = lapCount?.Latest?.CurrentLap ?? 0,
                totalLaps = lapCount?.Latest?.TotalLaps ?? 0,
                totalDrivers = timingData?.Latest?.Lines?.Count ?? 0,
                activeDrivers = timingData?.Latest?.Lines?.Values?.Count(d => d.Retired != true && d.KnockedOut != true) ?? 0,
                driversInPit = timingData?.Latest?.Lines?.Values?.Count(d => d.InPit == true) ?? 0,
                timestamp = DateTime.UtcNow
            };

            return Results.Ok(info);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error getting session info: {ex.Message}");
        }
    }

    private static int GetLeaderLap(TimingDataProcessor timingData, LapCountProcessor lapCount)
    {
        var leader = timingData?.Latest?.Lines?.Values
            .Where(d => d.Line == 1)
            .FirstOrDefault();
            
        return leader?.NumberOfLaps ?? lapCount?.Latest?.CurrentLap ?? 1;
    }

    private static IResult GetDebugInfo(
        TimingDataProcessor timingData,
        DriverListProcessor driverList,
        LapCountProcessor lapCount,
        CircularTrackPositionCalculator positionCalculator)
    {
        try
        {
            if (timingData?.Latest?.Lines == null)
            {
                return Results.Ok(new { message = "No timing data available" });
            }

            var debugInfo = new List<object>();
            var currentLap = lapCount?.Latest?.CurrentLap ?? 1;
            var leaderLap = GetLeaderLap(timingData, lapCount);

            // Get first few drivers for debugging
            foreach (var (driverNumber, timingLine) in timingData.Latest.Lines.Take(5))
            {
                var driver = driverList?.Latest?.GetValueOrDefault(driverNumber);
                if (driver == null) continue;

                var circularPosition = positionCalculator.CalculatePosition(timingLine, currentLap, leaderLap);
                var trackProgress = positionCalculator.GetTrackProgress(timingLine);

                var debug = new
                {
                    driverNumber,
                    driverTla = driver.Tla,
                    racePosition = timingLine.Line,
                    sectors = timingLine.Sectors?.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value?.Value ?? "null"
                    ),
                    trackProgress,
                    angle = circularPosition.Angle,
                    radialPosition = circularPosition.RadialPosition,
                    lapNumber = circularPosition.LapNumber
                };

                debugInfo.Add(debug);
            }

            return Results.Ok(new { drivers = debugInfo, currentLap, leaderLap });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Debug error: {ex.Message}");
        }
    }
}