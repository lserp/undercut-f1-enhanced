using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Console.Models;
using UndercutF1.Console.Services;
using UndercutF1.Data;

namespace UndercutF1.Console.Display;

/// <summary>
/// Display for the circular track visualization showing drivers as dots moving around a circle
/// </summary>
public class CircularTrackDisplay(
    State state,
    CommonDisplayComponents common,
    TimingDataProcessor timingData,
    DriverListProcessor driverList,
    SessionInfoProcessor sessionInfoProcessor,
    LapCountProcessor lapCount,
    PositionDataProcessor positionData,
    CircularTrackPositionCalculator positionCalculator,
    CircularTrackRenderer renderer,
    CircularTrackGraphicsRenderer graphicsRenderer
) : IDisplay
{
    public Screen Screen => Screen.CircularTrack;

    private const int INFO_PANEL_WIDTH = 25;

    public Task<IRenderable> GetContentAsync()
    {
        var statusPanel = common.GetStatusPanel();
        var circleVisualization = CreateCircleVisualization();
        var infoPanel = CreateInfoPanel();
        var legendPanel = graphicsRenderer.CreateLegend();

        var layout = new Layout("Root").SplitRows(
            new Layout("Header", CreateHeaderPanel()).Size(3),
            new Layout("Content")
                .SplitColumns(
                    new Layout("Circle", circleVisualization),
                    new Layout("Info")
                        .SplitRows(
                            new Layout("Status", statusPanel).Size(6),
                            new Layout("Legend", legendPanel).Size(8),
                            new Layout("Details", infoPanel)
                        )
                        .Size(INFO_PANEL_WIDTH)
                )
        );

        return Task.FromResult<IRenderable>(layout);
    }

    /// <summary>
    /// Creates the header panel with session information
    /// </summary>
    private IRenderable CreateHeaderPanel()
    {
        var currentLap = lapCount?.Latest?.CurrentLap ?? 0;
        var totalLaps = lapCount?.Latest?.TotalLaps ?? 0;
        var sessionName = sessionInfoProcessor?.Latest?.Name ?? "Unknown Session";
        var sessionType = sessionInfoProcessor?.Latest?.Type ?? "Unknown";
        var meetingName = sessionInfoProcessor?.Latest?.Meeting?.Name ?? "";
        
        // Add track/meeting info if available
        var locationInfo = !string.IsNullOrEmpty(meetingName) ? $" - {meetingName}" : "";
        
        var headerText = totalLaps > 0 
            ? $"🏁 Circular Track View - LAP {currentLap}/{totalLaps} - {sessionName} ({sessionType}){locationInfo}"
            : $"🏁 Circular Track View - {sessionName} ({sessionType}){locationInfo}";

        // Add data freshness indicator
        var dataAge = GetDataFreshnessIndicator();
        if (!string.IsNullOrEmpty(dataAge))
        {
            headerText += $" {dataAge}";
        }

        return new Panel(new Text(headerText).Centered())
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };
    }

    /// <summary>
    /// Creates the main circle visualization
    /// </summary>
    private IRenderable CreateCircleVisualization()
    {
        try
        {
            if (timingData?.Latest?.Lines == null || !timingData.Latest.Lines.Any())
            {
                return CreateErrorPanel("Waiting for timing data...", "⏳");
            }

            var driverPositions = CalculateDriverPositions();
            
            if (!driverPositions.Any())
            {
                return CreateErrorPanel("No active drivers on track", "🏎️");
            }

            // Check for terminal size constraints
            var terminalWidth = Terminal.Size.Width - INFO_PANEL_WIDTH - 5;
            var terminalHeight = Terminal.Size.Height - 10;
            
            if (terminalWidth < 30 || terminalHeight < 15)
            {
                return CreateErrorPanel("Terminal too small for circular display", "📏");
            }

            // Create position lookup for accurate position display
            var positionLookup = CreatePositionLookup();

            // Try graphics renderer first, fallback to ASCII if it fails
            try
            {
                return graphicsRenderer.RenderCircle(driverPositions, terminalWidth, terminalHeight, positionLookup);
            }
            catch (Exception)
            {
                // Fallback to ASCII renderer if graphics fails
                return renderer.RenderCircle(driverPositions, terminalWidth, terminalHeight, positionLookup);
            }
        }
        catch (Exception ex)
        {
            return CreateErrorPanel($"Error rendering circle: {ex.Message}", "❌");
        }
    }

    /// <summary>
    /// Calculates driver positions for the circular visualization
    /// </summary>
    private List<DriverPosition> CalculateDriverPositions()
    {
        var positions = new List<DriverPosition>();
        
        try
        {
            if (timingData?.Latest?.Lines == null)
                return positions;

            var currentLap = lapCount?.Latest?.CurrentLap ?? 1;
            var leaderLap = GetLeaderLap();

            foreach (var (driverNumber, timingLine) in timingData.Latest.Lines)
            {
                try
                {
                    // Validate driver data
                    if (string.IsNullOrEmpty(driverNumber) || timingLine == null)
                        continue;

                    var driver = driverList?.Latest?.GetValueOrDefault(driverNumber);
                    if (driver == null) continue;

                    // Skip retired or knocked out drivers
                    if (timingLine.Retired == true || timingLine.KnockedOut == true) continue;

                    var circularPosition = positionCalculator.CalculatePosition(timingLine, currentLap, leaderLap);
                    var status = positionCalculator.GetDriverStatus(timingLine);

                    // Validate calculated position
                    if (circularPosition.Angle < 0 || circularPosition.Angle >= 360)
                    {
                        // Reset to safe default if calculation is invalid
                        circularPosition = circularPosition with { Angle = 0 };
                    }

                    var driverPosition = new DriverPosition
                    {
                        DriverNumber = driverNumber,
                        DriverTla = driver.Tla ?? driverNumber.Substring(0, Math.Min(3, driverNumber.Length)),
                        TeamColor = $"#{driver.TeamColour ?? "FFFFFF"}",
                        Position = circularPosition,
                        Status = status,
                        GapToLeader = timingLine.GapToLeader ?? "N/A",
                        IntervalAhead = timingLine.IntervalToPositionAhead?.Value ?? "N/A"
                    };

                    positions.Add(driverPosition);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other drivers
                    // In a real application, you might want to use proper logging
                    System.Diagnostics.Debug.WriteLine($"Error processing driver {driverNumber}: {ex.Message}");
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but return empty list to prevent crash
            System.Diagnostics.Debug.WriteLine($"Error calculating driver positions: {ex.Message}");
            return new List<DriverPosition>();
        }

        return positions.OrderBy(p => p.Position.Angle).ToList();
    }

    /// <summary>
    /// Gets the leader's current lap for multi-lap calculations
    /// </summary>
    private int GetLeaderLap()
    {
        var leader = timingData?.Latest?.Lines?.Values
            .Where(d => d.Line == 1)
            .FirstOrDefault();
            
        return leader?.NumberOfLaps ?? lapCount?.Latest?.CurrentLap ?? 1;
    }

    /// <summary>
    /// Creates the information panel showing driver details and session info
    /// </summary>
    private IRenderable CreateInfoPanel()
    {
        try
        {
            var drivers = CalculateDriverPositions();
            
            if (!drivers.Any())
            {
                return new Panel(new Markup("[dim]No driver data available[/]"))
                {
                    Header = new PanelHeader("Driver Info"),
                    Border = BoxBorder.Rounded
                };
            }

            // Show top 5 drivers by position
            var topDrivers = drivers
                .OrderBy(d => GetRacePosition(d.DriverNumber))
                .Take(5)
                .ToList();

            var content = new List<IRenderable>
            {
                new Markup("[bold]🏎️ TOP POSITIONS[/]"),
                new Text("")
            };

            foreach (var driver in topDrivers)
            {
                try
                {
                    var position = GetRacePosition(driver.DriverNumber);
                    var statusIcon = GetStatusIcon(driver.Status);
                    var progressPercent = Math.Clamp(driver.Position.TrackProgress * 100, 0, 100).ToString("F1");
                    
                    content.Add(new Markup($"{statusIcon} [bold]P{position}[/] {driver.DriverTla}"));
                    content.Add(new Markup($"   Gap: {driver.GapToLeader}"));
                    content.Add(new Markup($"   Progress: {progressPercent}%"));
                    content.Add(new Text(""));
                }
                catch (Exception ex)
                {
                    // Skip problematic driver but continue with others
                    System.Diagnostics.Debug.WriteLine($"Error displaying driver {driver.DriverTla}: {ex.Message}");
                    continue;
                }
            }

            // Add session statistics with error handling
            content.Add(new Markup("[bold]📊 SESSION INFO[/]"));
            content.Add(new Text(""));
            
            var totalDrivers = drivers.Count;
            var onTrack = drivers.Count(d => d.Status == DriverStatus.OnTrack);
            var inPit = drivers.Count(d => d.Status == DriverStatus.InPit);
            var pitOut = drivers.Count(d => d.Status == DriverStatus.PitOut);
            var retired = timingData?.Latest?.Lines?.Values?.Count(d => d.Retired == true) ?? 0;
            
            content.Add(new Markup($"Active drivers: {totalDrivers}"));
            content.Add(new Markup($"🏎️ On track: {onTrack}"));
            content.Add(new Markup($"🔧 In pit: {inPit}"));
            if (pitOut > 0)
                content.Add(new Markup($"🏁 Pit out: {pitOut}"));
            if (retired > 0)
                content.Add(new Markup($"❌ Retired: {retired}"));
                
            // Add lap completion info for race sessions with validation
            var totalLaps = lapCount?.Latest?.TotalLaps ?? 0;
            var currentLap = lapCount?.Latest?.CurrentLap ?? 0;
            
            if (totalLaps > 0 && currentLap >= 0)
            {
                content.Add(new Text(""));
                content.Add(new Markup("[bold]🏁 LAP PROGRESS[/]"));
                var progress = Math.Clamp((double)currentLap / totalLaps * 100, 0, 100);
                var remaining = Math.Max(0, totalLaps - currentLap);
                content.Add(new Markup($"Progress: {progress:F1}%"));
                content.Add(new Markup($"Remaining: {remaining} laps"));
            }

            return new Panel(new Rows(content))
            {
                Header = new PanelHeader("Driver Info"),
                Border = BoxBorder.Rounded
            };
        }
        catch (Exception ex)
        {
            return new Panel(new Markup($"[red]Error creating info panel: {ex.Message}[/]"))
            {
                Header = new PanelHeader("Driver Info"),
                Border = BoxBorder.Rounded
            };
        }
    }

    /// <summary>
    /// Gets the race position for a driver
    /// </summary>
    private int GetRacePosition(string driverNumber)
    {
        var timingLine = timingData?.Latest?.Lines?.GetValueOrDefault(driverNumber);
        return timingLine?.Line ?? 99;
    }

    /// <summary>
    /// Gets a status icon for display
    /// </summary>
    private string GetStatusIcon(DriverStatus status) => status switch
    {
        DriverStatus.OnTrack => "🏎️",
        DriverStatus.InPit => "🔧",
        DriverStatus.PitOut => "🏁",
        DriverStatus.Retired => "❌",
        DriverStatus.OffTrack => "⚠️",
        DriverStatus.Stopped => "🛑",
        _ => "🏎️"
    };

    /// <summary>
    /// Creates a lookup dictionary for driver positions
    /// </summary>
    private Dictionary<string, int> CreatePositionLookup()
    {
        var lookup = new Dictionary<string, int>();
        
        if (timingData?.Latest?.Lines != null)
        {
            foreach (var (driverNumber, timingLine) in timingData.Latest.Lines)
            {
                if (timingLine.Line.HasValue)
                {
                    lookup[driverNumber] = timingLine.Line.Value;
                }
            }
        }
        
        return lookup;
    }

    /// <summary>
    /// Gets a data freshness indicator for the header
    /// </summary>
    private string GetDataFreshnessIndicator()
    {
        if (timingData?.Latest == null)
        {
            return "[red]● NO DATA[/]";
        }

        // Check if we have recent data (this is a simplified check)
        var hasActiveDrivers = timingData.Latest.Lines.Any(l => 
            l.Value.Retired != true && l.Value.KnockedOut != true);
            
        return !hasActiveDrivers ? "[yellow]● STALE DATA[/]" : "[green]● LIVE[/]";
    }

    /// <summary>
    /// Creates a standardized error panel
    /// </summary>
    private IRenderable CreateErrorPanel(string message, string icon) => new Panel(new Markup($"[yellow]{icon} {message}[/]"))
    {
        Header = new PanelHeader("🏁 Live Track Positions"),
        Border = BoxBorder.Rounded,
        Expand = true
    };
}