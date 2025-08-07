using SkiaSharp;
using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Console.Graphics;
using UndercutF1.Console.Models;

namespace UndercutF1.Console.Services;

/// <summary>
/// Renders the circular track visualization using SkiaSharp graphics
/// </summary>
public class CircularTrackGraphicsRenderer
{
    private const int DEFAULT_SIZE = 400;
    private const int DRIVER_DOT_SIZE = 12;
    private const int TRACK_WIDTH = 8;
    private const float COLLISION_THRESHOLD = 15.0f; // Pixels

    /// <summary>
    /// Renders the circular track with driver positions using graphics
    /// </summary>
    /// <param name="positions">List of driver positions to render</param>
    /// <param name="terminalWidth">Available terminal width in characters</param>
    /// <param name="terminalHeight">Available terminal height in characters</param>
    /// <param name="positionLookup">Optional lookup for driver race positions</param>
    /// <returns>Renderable content for the circular track</returns>
    public IRenderable RenderCircle(List<DriverPosition> positions, int terminalWidth, int terminalHeight, Dictionary<string, int>? positionLookup = null)
    {
        try
        {
            // Calculate optimal image size based on terminal dimensions
            var imageSize = CalculateOptimalImageSize(terminalWidth, terminalHeight);
            
            // Create the image with a more compatible format
            var imageInfo = new SKImageInfo(imageSize, imageSize, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(imageInfo);
            
            ArgumentNullException.ThrowIfNull(surface, "Failed to create SkiaSharp surface - SkiaSharp may not be properly initialized");
            
            var canvas = surface.Canvas;
            
            // Clear background
            canvas.Clear(SKColor.Parse("#1a1a1a")); // Dark background
            
            // Apply collision avoidance
            var adjustedPositions = ApplyCollisionAvoidance(positions, imageSize);
            
            // Draw the track circle
            DrawTrackCircle(canvas, imageSize);
            
            // Draw start/finish line
            DrawStartFinishLine(canvas, imageSize);
            
            // Draw driver positions
            DrawDriverPositions(canvas, adjustedPositions, imageSize);
            
            // Create the image and convert to appropriate format
            using var image = surface.Snapshot();
            
            // Calculate terminal cell dimensions for the image
            var cellWidth = Math.Min(terminalWidth - 5, 50);
            var cellHeight = Math.Min(terminalHeight - 10, 25);
            
            // Create the graphics sequence based on terminal capabilities
            var graphicsSequence = CreateGraphicsSequence(image, cellHeight, cellWidth);
            
            // Create layout with image and driver info
            var content = new List<IRenderable>
            {
                new Markup(graphicsSequence),
                new Text(""),
                CreateDriverIdentificationPanel(adjustedPositions, positionLookup)
            };
            
            return new Panel(new Rows(content))
            {
                Header = new PanelHeader("🏁 Live Track Positions"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0, 1, 0)
            };
        }
        catch (Exception ex)
        {
            // Fallback to error message if graphics rendering fails
            return new Panel(new Markup($"[red]Graphics rendering failed: {ex.Message}[/]"))
            {
                Header = new PanelHeader("🏁 Live Track Positions"),
                Border = BoxBorder.Rounded,
                Expand = true
            };
        }
    }

    /// <summary>
    /// Calculates optimal image size based on terminal dimensions
    /// </summary>
    private int CalculateOptimalImageSize(int terminalWidth, int terminalHeight)
    {
        // Base size on terminal dimensions but keep it reasonable
        var maxSize = Math.Min(terminalWidth * 8, terminalHeight * 16); // Approximate pixel conversion
        return Math.Clamp(maxSize, 300, 600);
    }

    /// <summary>
    /// Draws the main track circle
    /// </summary>
    private void DrawTrackCircle(SKCanvas canvas, int imageSize)
    {
        var center = imageSize / 2f;
        var radius = (imageSize / 2f) - 50; // Leave margin
        
        // Draw outer track edge
        using var outerPaint = new SKPaint
        {
            Color = SKColor.Parse("#ffffff"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = TRACK_WIDTH,
            IsAntialias = true
        };
        canvas.DrawCircle(center, center, radius, outerPaint);
        
        // Draw inner track edge
        using var innerPaint = new SKPaint
        {
            Color = SKColor.Parse("#cccccc"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawCircle(center, center, radius - TRACK_WIDTH, innerPaint);
    }

    /// <summary>
    /// Draws the start/finish line
    /// </summary>
    private void DrawStartFinishLine(SKCanvas canvas, int imageSize)
    {
        var center = imageSize / 2f;
        var radius = (imageSize / 2f) - 50;
        
        // Draw start/finish line at the top (0 degrees)
        using var paint = new SKPaint
        {
            Color = SKColor.Parse("#ffff00"), // Yellow
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            IsAntialias = true
        };
        
        var startX = center;
        var startY = center - radius - TRACK_WIDTH;
        var endY = center - radius + TRACK_WIDTH;
        
        canvas.DrawLine(startX, startY, startX, endY, paint);
        
        // Add "S/F" text
        using var textPaint = new SKPaint
        {
            Color = SKColor.Parse("#ffff00"),
            TextSize = 16,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Titillium Web", SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) 
                ?? SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Upright)
        };
        
        canvas.DrawText("S/F", startX - 12, startY - 10, textPaint);
    }

    /// <summary>
    /// Draws driver positions on the track
    /// </summary>
    private void DrawDriverPositions(SKCanvas canvas, List<DriverPosition> positions, int imageSize)
    {
        var center = imageSize / 2f;
        var baseRadius = (imageSize / 2f) - 50;
        
        foreach (var driver in positions)
        {
            // Calculate position on circle - always place drivers ON the track circle
            var angle = (driver.Position.Angle - 90) * Math.PI / 180.0; // -90 to start at top
            var radius = baseRadius; // Use full radius to place drivers on the track circle
            
            var x = (float)(center + radius * Math.Cos(angle));
            var y = (float)(center + radius * Math.Sin(angle));
            
            // Parse team color
            var teamColor = ParseTeamColor(driver.TeamColor);
            
            // Adjust visual appearance for lapped drivers
            var isLapped = driver.Position.RadialPosition < 1.0;
            var dotSize = isLapped ? DRIVER_DOT_SIZE * 0.8f : DRIVER_DOT_SIZE; // Smaller dots for lapped drivers
            var opacity = isLapped ? 0.7f : 0.9f; // More transparent for lapped drivers
            
            // Draw solid driver dot (no border for simplicity)
            using var dotPaint = new SKPaint
            {
                Color = teamColor.WithAlpha((byte)(255 * opacity)),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            
            canvas.DrawCircle(x, y, dotSize / 2f, dotPaint);
            
            // Calculate the best contrast color for the text
            var textColor = GetContrastColor(teamColor);
            var strokeColor = textColor.Red > 128 ? SKColors.Black : SKColors.White;
            
            // Draw driver number with stroke for better readability
            using var textStrokePaint = new SKPaint
            {
                Color = strokeColor,
                TextSize = 11,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Typeface = SKTypeface.FromFamilyName("Titillium Web", SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) 
                    ?? SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Upright)
            };
            
            // Draw driver number fill
            using var textPaint = new SKPaint
            {
                Color = textColor,
                TextSize = 11,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Style = SKPaintStyle.Fill,
                Typeface = SKTypeface.FromFamilyName("Titillium Web", SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) 
                    ?? SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Upright)
            };
            
            // Draw text with stroke first, then fill for better readability
            canvas.DrawText(driver.DriverNumber, x, y + 3, textStrokePaint);
            canvas.DrawText(driver.DriverNumber, x, y + 3, textPaint);
            
            // Note: DRS indicators are primarily handled in the web interface
            // The graphics renderer focuses on the core visualization
        }
    }

    /// <summary>
    /// Applies collision avoidance to prevent driver overlap
    /// </summary>
    private List<DriverPosition> ApplyCollisionAvoidance(List<DriverPosition> positions, int imageSize)
    {
        if (!positions.Any())
            return positions;

        var adjustedPositions = new List<DriverPosition>();
        var center = imageSize / 2f;
        var baseRadius = (imageSize / 2f) - 50;
        
        foreach (var position in positions.OrderBy(p => p.Position.Angle))
        {
            var adjustedAngle = position.Position.Angle;
            var maxAdjustments = 10;
            var adjustmentCount = 0;
            
            bool hasCollision;
            do
            {
                hasCollision = false;
                var radius = baseRadius; // Use full radius to place drivers on the track circle
                var angle = (adjustedAngle - 90) * Math.PI / 180.0;
                var x = (float)(center + radius * Math.Cos(angle));
                var y = (float)(center + radius * Math.Sin(angle));
                
                foreach (var placed in adjustedPositions)
                {
                    var placedRadius = baseRadius; // Use full radius for placed drivers too
                    var placedAngle = (placed.Position.Angle - 90) * Math.PI / 180.0;
                    var placedX = (float)(center + placedRadius * Math.Cos(placedAngle));
                    var placedY = (float)(center + placedRadius * Math.Sin(placedAngle));
                    
                    var distance = Math.Sqrt(Math.Pow(x - placedX, 2) + Math.Pow(y - placedY, 2));
                    
                    if (distance < COLLISION_THRESHOLD)
                    {
                        adjustedAngle += 5; // Adjust by 5 degrees
                        if (adjustedAngle >= 360)
                            adjustedAngle -= 360;
                        hasCollision = true;
                        adjustmentCount++;
                        break;
                    }
                }
            } while (hasCollision && adjustmentCount < maxAdjustments);
            
            var adjustedPosition = position with
            {
                Position = position.Position with { Angle = adjustedAngle }
            };
            
            adjustedPositions.Add(adjustedPosition);
        }
        
        return adjustedPositions;
    }

    /// <summary>
    /// Parses team color from hex string
    /// </summary>
    private SKColor ParseTeamColor(string teamColor)
    {
        try
        {
            var colorString = teamColor.TrimStart('#');
            if (colorString.Length == 6)
            {
                return SKColor.Parse(colorString);
            }
        }
        catch
        {
            // Fallback to white if parsing fails
        }
        
        return SKColors.White;
    }

    /// <summary>
    /// Creates the appropriate graphics sequence for the terminal
    /// </summary>
    private string CreateGraphicsSequence(SKImage image, int height, int width)
    {
        // Detect terminal type from environment variables
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        var term = Environment.GetEnvironmentVariable("TERM");
        
        // For now, just use iTerm2 format to eliminate variables
        var result = CreateITerm2Sequence(image, height, width);
        return result;
    }

    /// <summary>
    /// Creates iTerm2 graphics sequence
    /// </summary>
    private string CreateITerm2Sequence(SKImage image, int height, int width)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var base64Image = Convert.ToBase64String(data.ToArray());
        return TerminalGraphics.ITerm2GraphicsSequence(height, width, base64Image);
    }

    /// <summary>
    /// Creates Kitty graphics sequence
    /// </summary>
    private string CreateKittySequence(SKImage image, int height, int width)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var base64Image = Convert.ToBase64String(data.ToArray());
        return string.Join("", TerminalGraphics.KittyGraphicsSequence(height, width, base64Image));
    }

    /// <summary>
    /// Creates Sixel graphics sequence
    /// </summary>
    private string CreateSixelSequence(SKImage image)
    {
        try
        {
            // Convert image to pixel array for Sixel encoding
            using var pixmap = image.PeekPixels();
            var pixels = new SKColor[pixmap.Width * pixmap.Height];
            
            for (var y = 0; y < pixmap.Height; y++)
            {
                for (var x = 0; x < pixmap.Width; x++)
                {
                    pixels[y * pixmap.Width + x] = pixmap.GetPixelColor(x, y);
                }
            }
            
            var sixelData = Sixel.ImageToSixel(pixels, pixmap.Width);
            return TerminalGraphics.SixelGraphicsSequence(sixelData);
        }
        catch
        {
            // Fallback to iTerm2 if Sixel conversion fails
            return CreateITerm2Sequence(image, 25, 50);
        }
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
            .OrderBy(p => positionLookup?.GetValueOrDefault(p.DriverNumber) ?? int.Parse(p.DriverNumber))
            .Take(8) // Show top 8 to fit better with graphics
            .ToList();

        foreach (var driver in driversToShow)
        {
            var statusIcon = GetStatusIcon(driver.Status);
            var position = positionLookup?.GetValueOrDefault(driver.DriverNumber) ?? int.Parse(driver.DriverNumber);
            
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
        var columns = driverTags.Chunk(4).Select(chunk => new Rows(chunk)).ToArray();
        
        return columns.Length > 1 
            ? new Columns(columns)
            : new Rows(driverTags);
    }

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
    /// Darkens a color by the specified factor
    /// </summary>
    private SKColor DarkenColor(SKColor color, float factor)
    {
        var r = (byte)(color.Red * (1 - factor));
        var g = (byte)(color.Green * (1 - factor));
        var b = (byte)(color.Blue * (1 - factor));
        return new SKColor(r, g, b, color.Alpha);
    }

    /// <summary>
    /// Gets the best contrast color (black or white) for the given background color
    /// </summary>
    private SKColor GetContrastColor(SKColor backgroundColor)
    {
        // Calculate luminance
        var luminance = (0.299 * backgroundColor.Red + 0.587 * backgroundColor.Green + 0.114 * backgroundColor.Blue) / 255;
        
        // Return black for light colors, white for dark colors
        return luminance > 0.5 ? SKColors.Black : SKColors.White;
    }



    /// <summary>
    /// Creates a legend explaining the driver status symbols
    /// </summary>
    public IRenderable CreateLegend()
    {
        var legendItems = new List<string>
        {
            "🏎️ On Track",
            "🔧 In Pit",
            "🏁 Pit Out", 
            "⚠️ Off Track",
            "❌ Retired",
            "🛑 Stopped"
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