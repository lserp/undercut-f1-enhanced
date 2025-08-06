using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Data;

namespace UndercutF1.Console.Display;

public class CircleOfDoomDisplay(
    State state,
    TimingDataProcessor timingData,
    PositionDataProcessor positionData,
    DriverListProcessor driverList,
    SessionInfoProcessor sessionInfo,
    LapCountProcessor lapCount,
    PitStopSeriesProcessor pitStopSeries,
    PitStopTimeProvider pitStopTimeProvider,
    TerminalInfoProvider terminalInfo,
    ILogger<CircleOfDoomDisplay> logger
) : IDisplay
{
    public Screen Screen => Screen.CircleOfDoom;

    private const int CircleSize = 25; // ASCII circle radius in characters
    private const int CenterX = CircleSize;
    private const int CenterY = CircleSize;
    
    private static bool _showPitProjections = true; // Toggle pit projections

    public static void TogglePitProjections() => _showPitProjections = !_showPitProjections;

    public Task<IRenderable> GetContentAsync()
    {
        var drivers = driverList?.Latest?.Values?.ToList() ?? [];
        var currentLap = lapCount?.Latest?.CurrentLap;
        var totalLaps = lapCount?.Latest?.TotalLaps;
        var sessionName = sessionInfo?.Latest?.Name ?? "Unknown Session";

        // Create the header text
        var headerText = $"LAP {currentLap ?? 0}/{totalLaps ?? 0} Circle of Doom - {sessionName}";

        // Ensure cursor is within bounds
        var activeDriverCount = drivers?.Where(d => d?.RacingNumber != null).Take(20).Count() ?? 0;
        if (state.CursorOffset >= activeDriverCount && activeDriverCount > 0)
        {
            state.CursorOffset = activeDriverCount - 1;
        }
        
        // Generate real-time ASCII circle visualization
        var circleVisualization = CreateRealTimeCircleVisualization(drivers, currentLap ?? 1);
        
        // Create the layout
        var layout = new Layout("Root").SplitRows(
            new Layout("Header", new Panel(new Text(headerText).Centered()).Expand()).Size(3),
            new Layout("Content").SplitColumns(
                new Layout("Circle", circleVisualization),
                new Layout("Info", CreateInfoPanel(drivers))
            )
        );

        return Task.FromResult<IRenderable>(new Panel(layout).Expand().RoundedBorder());
    }

    public async Task PostContentDrawAsync() =>
        // ASCII-based visualization doesn't need post-draw operations
        // All rendering is handled in GetContentAsync() for real-time updates
        await Task.CompletedTask;

    private IRenderable CreateRealTimeCircleVisualization(List<DriverListDataPoint.Driver> drivers, int currentLap)
    {
        var activeDrivers = drivers?.Where(d => d?.RacingNumber != null).Take(20).ToList() ?? [];
        
        if (!activeDrivers.Any())
        {
            return new Panel(new Markup("[yellow]No driver data available[/]"))
            {
                Header = new PanelHeader("Circle of Doom - Pit Strategy Analysis"),
                Border = BoxBorder.Rounded
            };
        }

        // Create text-based pit strategy overview
        var content = new List<IRenderable>
        {
            new Markup("[bold yellow]🏁 CIRCLE OF DOOM - PIT STRATEGY ANALYSIS[/]"),
            new Markup("[dim]Use [bold]▲/▼[/] to select driver for detailed analysis[/]"),
            new Text(""),
            new Markup("[bold]📊 PIT STRATEGY OVERVIEW[/]"),
        };

        // Get track name for pit timing
        var trackName = sessionInfo.Latest.Meeting?.Name ?? "Unknown";
        var trackTiming = pitStopTimeProvider.GetTimingForTrack(trackName);
        
        content.Add(new Markup($"[bold]Track:[/] {trackName}"));
        content.Add(new Markup($"[bold]Estimated Pit Loss:[/] {trackTiming.TotalTimeLoss:F1}s"));
        content.Add(new Text(""));

        // Show drivers with their current status and pit projections
        content.Add(new Markup("[bold]🏎️ DRIVER STATUS & PIT PROJECTIONS[/]"));
        content.Add(new Text(""));

        foreach (var driver in activeDrivers)
        {
            var driverTiming = timingData.Latest.Lines.GetValueOrDefault(driver.RacingNumber!);
            var gap = driverTiming?.GapToLeader ?? "N/A";
            var interval = driverTiming?.IntervalToPositionAhead?.Value ?? "N/A";
            var pitStops = driverTiming?.NumberOfPitStops ?? 0;

            var statusIcon = driverTiming?.InPit == true ? "🔧" : 
                            driverTiming?.PitOut == true ? "🏁" : "🏎️";

            content.Add(new Markup($"{statusIcon} [bold]{driver.Tla}[/] ({driver.TeamName})"));
            content.Add(new Markup($"   Gap: {gap} | Interval: {interval} | Stops: {pitStops}"));
            
            // Show pit projection
            if (driverTiming?.InPit != true)
            {
                content.Add(new Markup($"   [dim]→ After pit: +{trackTiming.TotalTimeLoss:F1}s time loss[/]"));
            }
            
            content.Add(new Text(""));
        }

        return new Panel(new Rows(content))
        {
            Header = new PanelHeader($"🏎️ Lap {currentLap} - Pit Strategy Analysis"),
            Border = BoxBorder.Rounded
        };
    }







    private double CalculateTrackPosition(TimingDataPoint.Driver driverTiming)
    {
        if (!driverTiming.Line.HasValue) return 0.0;
        
        var basePosition = driverTiming.Line.Value;
        
        // Add intra-lap progress based on sector completion for real-time updates
        var sectorProgress = 0.0;
        if (driverTiming.Sectors != null && driverTiming.Sectors.Count > 0)
        {
            // Check sector completion (0-indexed keys)
            var sector1 = driverTiming.Sectors.GetValueOrDefault("0")?.Value != null;
            var sector2 = driverTiming.Sectors.GetValueOrDefault("1")?.Value != null;
            var sector3 = driverTiming.Sectors.GetValueOrDefault("2")?.Value != null;
            
            if (sector3) sectorProgress = 0.9;      // Almost complete lap
            else if (sector2) sectorProgress = 0.6; // 2/3 through lap  
            else if (sector1) sectorProgress = 0.3; // 1/3 through lap
            else sectorProgress = 0.1;              // Just started lap
        }
        
        // Combine race position with intra-lap progress for smooth movement
        var positionSegment = ((basePosition - 1) % 20) / 20.0;
        var segmentSize = 1.0 / 20.0;
        var totalPosition = positionSegment + (sectorProgress * segmentSize);
        
        // Wrap around if > 1.0
        if (totalPosition >= 1.0) totalPosition -= 1.0;
        
        return totalPosition;
    }

    private IRenderable CreateInfoPanel(List<DriverListDataPoint.Driver> drivers)
    {
        var activeDrivers = drivers?.Where(d => d?.RacingNumber != null).Take(20).ToList() ?? [];
        var selectedDriver = state.CursorOffset < activeDrivers.Count ? activeDrivers[state.CursorOffset] : null;
        
        if (selectedDriver?.RacingNumber == null)
        {
            return new Panel(new Markup("[dim]Use ▲/▼ to select a driver for detailed pit strategy analysis[/]"))
            {
                Header = new PanelHeader("Driver Selection"),
                Border = BoxBorder.Rounded
            };
        }

        var driverTiming = timingData.Latest.Lines.GetValueOrDefault(selectedDriver.RacingNumber);
        var trackName = sessionInfo.Latest.Meeting?.Name ?? "Unknown";
        var timeLoss = pitStopTimeProvider.GetTotalTimeLoss(trackName, pitStopSeries, selectedDriver.RacingNumber);
        var trackTiming = pitStopTimeProvider.GetTimingForTrack(trackName);

        // Calculate real-time track position
        var trackPosition = driverTiming != null ? CalculateTrackPosition(driverTiming) : 0.0;
        var trackPercentage = (trackPosition * 100).ToString("F1");

        var info = new List<IRenderable>
        {
            new Markup($"[bold]{selectedDriver.FullName}[/] (#{selectedDriver.RacingNumber})"),
            new Markup($"[bold]Team:[/] {selectedDriver.TeamName}"),
            new Text(""),
            new Markup("[bold]📊 LIVE POSITION[/]"),
            new Markup($"Race position: P{driverTiming?.Line ?? 0}"),
            new Markup($"Track progress: {trackPercentage}%"),
            new Markup($"Gap to leader: {driverTiming?.GapToLeader ?? "N/A"}"),
            new Markup($"Interval ahead: {driverTiming?.IntervalToPositionAhead?.Value ?? "N/A"}"),
            new Markup($"Pit stops: {driverTiming?.NumberOfPitStops ?? 0}"),
            new Text(""),
            new Markup("[bold]🔧 PIT STRATEGY[/]"),
            new Markup($"Track: {trackName}"),
            new Markup($"[yellow]Total time loss: {timeLoss:F1}s[/]"),
            new Markup($"  • Stop time: {trackTiming.StopTime:F1}s"),
            new Markup($"  • Pit lane: {trackTiming.PitLaneTime:F1}s"),
            new Markup($"  • Transition: {trackTiming.TransitionTime:F1}s"),
            new Text(""),
            new Markup("[bold]🎯 REAL-TIME PROJECTION[/]"),
            new Markup($"After pit stop: [red]+{timeLoss:F1}s behind current position[/]"),
        };

        // Add pit window analysis if we have gap data
        if (driverTiming?.GapToLeader != null && double.TryParse(driverTiming.GapToLeader.TrimStart('+'), out var gapSeconds))
        {
            var projectedGap = gapSeconds + timeLoss;
            info.Add(new Markup($"Projected gap to leader: [yellow]+{projectedGap:F1}s[/]"));
        }

        // Show current status
        var status = driverTiming?.InPit == true ? "[red]IN PIT[/]" : 
                    driverTiming?.PitOut == true ? "[green]PIT OUT[/]" : 
                    "[white]ON TRACK[/]";
        info.Add(new Markup($"Status: {status}"));

        return new Panel(new Rows(info))
        {
            Header = new PanelHeader($"🏎️ {selectedDriver.Tla} - Live Analysis"),
            Border = BoxBorder.Rounded
        };
    }
}