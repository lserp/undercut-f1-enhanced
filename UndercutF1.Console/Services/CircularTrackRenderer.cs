using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Console.Models;

namespace UndercutF1.Console.Services;

/// <summary>
/// Renders the circular track visualization using ASCII characters
/// </summary>
public class CircularTrackRenderer
{
    private const int MIN_CIRCLE_RADIUS = 8;
    private const int MAX_CIRCLE_RADIUS = 20;
    private const double COLLISION_THRESHOLD = 0.1; // Minimum distance between drivers to avoid overlap

    /// <summary>
    /// Renders the circular track with driver positions
    /// </summary>
    /// <param name="positions">List of driver positions to render</param>
    /// <param name="terminalWidth">Available terminal width</param>
    /// <param name="terminalHeight">Available terminal height</param>
    /// <param name="positionLookup">Optional lookup for driver race positions</param>
    /// <returns>Renderable content for the circular track</returns>
    public IRenderable RenderCircle(List<DriverPosition> positions, int terminalWidth, int terminalHeight, Dictionary<string, int>? positionLookup = null)
    {
        var radius = CalculateOptimalRadius(terminalWidth, terminalHeight);
        var circleSize = radius * 2 + 1;
        
        // Create the ASCII circle grid
        var grid = CreateCircleGrid(radius);
        
        // Position drivers on the circle with collision avoidance
        var adjustedPositions = ApplyCollisionAvoidance(positions);
        
        // Place drivers on the grid
        foreach (var driverPos in adjustedPositions)
        {
            PlaceDriverOnGrid(grid, driverPos, radius);
        }
        
        // Convert grid to renderable content with driver identification
        return CreateRenderableFromGrid(grid, circleSize, adjustedPositions, positionLookup);
    }

    /// <summary>
    /// Calculates the optimal circle radius based on terminal dimensions
    /// </summary>
    private int CalculateOptimalRadius(int terminalWidth, int terminalHeight)
    {
        // Reserve space for info panels and borders
        var availableWidth = Math.Max(terminalWidth - 40, 20); // Reserve 40 chars for side panels
        var availableHeight = Math.Max(terminalHeight - 10, 16); // Reserve 10 lines for headers/footers
        
        // Calculate radius that fits in available space
        var maxRadiusFromWidth = availableWidth / 2;
        var maxRadiusFromHeight = availableHeight / 2;
        
        var radius = Math.Min(maxRadiusFromWidth, maxRadiusFromHeight);
        
        // Clamp to reasonable bounds
        return Math.Clamp(radius, MIN_CIRCLE_RADIUS, MAX_CIRCLE_RADIUS);
    }

    /// <summary>
    /// Creates a 2D grid representing the circle outline
    /// </summary>
    private char[,] CreateCircleGrid(int radius)
    {
        var size = radius * 2 + 1;
        var grid = new char[size, size];
        var centerX = radius;
        var centerY = radius;
        
        // Initialize grid with spaces
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                grid[y, x] = ' ';
            }
        }
        
        // Draw circle outline using Bresenham-like algorithm
        for (var angle = 0; angle < 360; angle += 2) // Step by 2 degrees for smoother circle
        {
            var radians = angle * Math.PI / 180.0;
            var x = (int)Math.Round(centerX + radius * Math.Sin(radians));
            var y = (int)Math.Round(centerY - radius * Math.Cos(radians)); // Negative because Y increases downward
            
            if (x >= 0 && x < size && y >= 0 && y < size)
            {
                grid[y, x] = GetCircleChar(angle);
            }
        }
        
        return grid;
    }

    /// <summary>
    /// Gets the appropriate character for the circle outline based on position
    /// </summary>
    private char GetCircleChar(int angle) => angle switch
    {
        >= 0 and < 45 => '│',      // Top
        >= 45 and < 135 => '─',    // Right
        >= 135 and < 225 => '│',   // Bottom
        >= 225 and < 315 => '─',   // Left
        _ => '│'                   // Top again
    };

    /// <summary>
    /// Applies collision avoidance to prevent driver overlap
    /// </summary>
    private List<DriverPosition> ApplyCollisionAvoidance(List<DriverPosition> positions)
    {
        if (!positions.Any())
            return positions;

        var adjustedPositions = new List<DriverPosition>();
        
        try
        {
            foreach (var position in positions.OrderBy(p => p.Position.Angle))
            {
                var adjustedAngle = Math.Clamp(position.Position.Angle, 0, 359.99);
                var maxAdjustments = 10; // Prevent infinite loops
                var adjustmentCount = 0;
                
                // Check for collisions with already placed drivers
                bool hasCollision;
                do
                {
                    hasCollision = false;
                    foreach (var placed in adjustedPositions)
                    {
                        var angleDiff = Math.Abs(adjustedAngle - placed.Position.Angle);
                        
                        // Handle wrap-around (e.g., 359° and 1° are close)
                        if (angleDiff > 180)
                            angleDiff = 360 - angleDiff;
                        
                        if (angleDiff < COLLISION_THRESHOLD * 360) // Convert threshold to degrees
                        {
                            // Adjust angle to avoid collision
                            adjustedAngle = placed.Position.Angle + (COLLISION_THRESHOLD * 360);
                            if (adjustedAngle >= 360)
                                adjustedAngle -= 360;
                            hasCollision = true;
                            adjustmentCount++;
                            break;
                        }
                    }
                } while (hasCollision && adjustmentCount < maxAdjustments);
                
                // Create adjusted position
                var adjustedPosition = position with
                {
                    Position = position.Position with { Angle = adjustedAngle }
                };
                
                adjustedPositions.Add(adjustedPosition);
            }
        }
        catch (Exception ex)
        {
            // If collision avoidance fails, return original positions
            System.Diagnostics.Debug.WriteLine($"Error in collision avoidance: {ex.Message}");
            return positions;
        }
        
        return adjustedPositions;
    }

    /// <summary>
    /// Places a driver marker on the grid at the calculated position
    /// </summary>
    private void PlaceDriverOnGrid(char[,] grid, DriverPosition driverPos, int radius)
    {
        var size = radius * 2 + 1;
        var centerX = radius;
        var centerY = radius;
        
        // Convert angle to grid coordinates
        var radians = driverPos.Position.Angle * Math.PI / 180.0;
        var effectiveRadius = radius * driverPos.Position.RadialPosition; // Adjust for multi-lap
        
        var x = (int)Math.Round(centerX + effectiveRadius * Math.Sin(radians));
        var y = (int)Math.Round(centerY - effectiveRadius * Math.Cos(radians));
        
        // Ensure coordinates are within bounds
        if (x >= 0 && x < size && y >= 0 && y < size)
        {
            // Use different characters based on driver status
            grid[y, x] = GetDriverChar(driverPos.Status);
        }
    }

    /// <summary>
    /// Gets the appropriate character to represent a driver based on their status
    /// </summary>
    private char GetDriverChar(DriverStatus status) => status switch
    {
        DriverStatus.OnTrack => '●',
        DriverStatus.InPit => '◐',
        DriverStatus.PitOut => '◉',
        DriverStatus.Retired => '×',
        DriverStatus.OffTrack => '○',
        DriverStatus.Stopped => '■',
        _ => '●'
    };

    /// <summary>
    /// Converts the character grid to a renderable Spectre.Console object with colored driver markers
    /// </summary>
    private IRenderable CreateRenderableFromGrid(char[,] grid, int size, List<DriverPosition> positions, Dictionary<string, int>? positionLookup = null)
    {
        var content = new List<IRenderable>();
        
        for (var y = 0; y < size; y++)
        {
            var lineSegments = new List<IRenderable>();
            
            for (var x = 0; x < size; x++)
            {
                var character = grid[y, x];
                
                // Check if this position has a driver
                var driverAtPosition = FindDriverAtGridPosition(positions, x, y, size / 2);
                
                if (driverAtPosition != null && character != ' ')
                {
                    // Create colored driver marker with team color using markup
                    var driverChar = GetDriverChar(driverAtPosition.Status);
                    try
                    {
                        var coloredMarker = $"[{driverAtPosition.TeamColor}]{driverChar}[/]";
                        lineSegments.Add(new Markup(coloredMarker));
                    }
                    catch
                    {
                        // Fallback to white if team color is invalid
                        lineSegments.Add(new Text(driverChar.ToString(), new Style(foreground: Color.White)));
                    }
                }
                else
                {
                    // Regular circle outline or space
                    var color = character == ' ' ? Color.Black : Color.Grey;
                    lineSegments.Add(new Text(character.ToString(), new Style(foreground: color)));
                }
            }
            
            content.Add(new Columns(lineSegments));
        }
        
        // Add driver identification below the circle
        content.Add(new Text(""));
        content.Add(CreateDriverIdentificationPanel(positions, positionLookup));
        
        return new Panel(new Rows(content))
        {
            Header = new PanelHeader("🏁 Live Track Positions"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };
    }

    /// <summary>
    /// Finds a driver at a specific grid position
    /// </summary>
    private DriverPosition? FindDriverAtGridPosition(List<DriverPosition> positions, int gridX, int gridY, int radius)
    {
        var centerX = radius;
        var centerY = radius;
        
        foreach (var driver in positions)
        {
            var radians = driver.Position.Angle * Math.PI / 180.0;
            var effectiveRadius = radius * driver.Position.RadialPosition;
            
            var driverX = (int)Math.Round(centerX + effectiveRadius * Math.Sin(radians));
            var driverY = (int)Math.Round(centerY - effectiveRadius * Math.Cos(radians));
            
            if (Math.Abs(driverX - gridX) <= 1 && Math.Abs(driverY - gridY) <= 1)
            {
                return driver;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Creates a panel showing driver identification with team colors
    /// </summary>
    private IRenderable CreateDriverIdentificationPanel(List<DriverPosition> positions, Dictionary<string, int>? positionLookup = null)
    {
        if (!positions.Any())
        {
            return new Text("");
        }

        var driverTags = new List<IRenderable>();
        
        // Show drivers in position order, but limit to prevent overflow
        var driversToShow = positions
            .OrderBy(p => positionLookup?.GetValueOrDefault(p.DriverNumber) ?? GetPositionFromDriverNumber(p.DriverNumber))
            .Take(10) // Show top 10 to prevent overflow
            .ToList();

        foreach (var driver in driversToShow)
        {
            var statusIcon = GetStatusIcon(driver.Status);
            var position = positionLookup?.GetValueOrDefault(driver.DriverNumber) ?? GetPositionFromDriverNumber(driver.DriverNumber);
            
            try
            {
                var driverTag = new Markup(
                    $"{statusIcon} [bold]P{position}[/] [{driver.TeamColor}]{driver.DriverNumber} {driver.DriverTla}[/]"
                );
                driverTags.Add(driverTag);
            }
            catch
            {
                // Fallback to plain text if team color is invalid
                var driverTag = new Markup(
                    $"{statusIcon} [bold]P{position}[/] [white]{driver.DriverNumber} {driver.DriverTla}[/]"
                );
                driverTags.Add(driverTag);
            }
        }

        // Arrange in columns to save space
        var columns = driverTags.Chunk(5).Select(chunk => new Rows(chunk)).ToArray();
        
        return columns.Length > 1 
            ? new Columns(columns)
            : new Rows(driverTags);
    }

    /// <summary>
    /// Gets position number from driver number (placeholder - would need timing data)
    /// </summary>
    private int GetPositionFromDriverNumber(string driverNumber) =>
        // This is a placeholder - in the actual display, this would come from timing data
        // For now, just parse the driver number as a rough approximation
        int.TryParse(driverNumber, out var num) ? num : 99;

    /// <summary>
    /// Gets a status icon for driver identification
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
    /// Creates a legend explaining the driver status symbols
    /// </summary>
    public IRenderable CreateLegend()
    {
        var legendItems = new List<string>
        {
            "● On Track",
            "◐ In Pit",
            "◉ Pit Out", 
            "○ Off Track",
            "× Retired",
            "■ Stopped"
        };
        
        var content = string.Join(Environment.NewLine, legendItems);
        
        return new Panel(new Markup(content))
        {
            Header = new PanelHeader("Legend"),
            Border = BoxBorder.Rounded,
            Width = 15
        };
    }
}