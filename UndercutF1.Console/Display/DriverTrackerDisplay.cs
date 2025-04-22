using Microsoft.Extensions.Options;
using SkiaSharp;
using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Data;

namespace UndercutF1.Console;

public class DriverTrackerDisplay(
    State state,
    TimingDataProcessor timingData,
    DriverListProcessor driverList,
    PositionDataProcessor positionData,
    CarDataProcessor carData,
    SessionInfoProcessor sessionInfo,
    TrackStatusProcessor trackStatus,
    ExtrapolatedClockProcessor extrapolatedClock,
    IDateTimeProvider dateTimeProvider,
    TerminalInfoProvider terminalInfo,
    IOptions<LiveTimingOptions> options
) : IDisplay
{
    private const int IMAGE_PADDING = 25;
    private const int TARGET_IMAGE_SIZE = 650;
    private const int LEFT_OFFSET = 17;
    private const int TOP_OFFSET = 0;
    private const int BOTTOM_OFFSET = 1;

    private static readonly SKPaint _trackLinePaint = new()
    {
        Color = SKColor.Parse("666666"),
        StrokeWidth = 4,
    };
    private static readonly SKPaint _cornerTextPaint = new()
    {
        Color = SKColor.Parse("DDDDDD"),
        TextSize = 14,
        Typeface = SKTypeface.FromFamilyName(
            "Consolas",
            weight: SKFontStyleWeight.SemiBold,
            width: SKFontStyleWidth.Normal,
            slant: SKFontStyleSlant.Upright
        ),
    };
    private static readonly SKPaint _errorPaint = new()
    {
        Color = SKColor.Parse("FF0000"),
        IsStroke = true,
        Typeface = _boldTypeface,
    };
    private static readonly SKTypeface _boldTypeface = SKTypeface.FromFamilyName(
        "Consolas",
        weight: SKFontStyleWeight.ExtraBold,
        width: SKFontStyleWidth.Normal,
        slant: SKFontStyleSlant.Upright
    );

    private TransformFactors? _transform = null;

    private string _trackMapControlSequence = string.Empty;

    public Screen Screen => Screen.DriverTracker;

    public Task<IRenderable> GetContentAsync()
    {
        var trackMapMessage = string.Empty;
        if (
            !terminalInfo.IsITerm2ProtocolSupported.Value
            && !terminalInfo.IsKittyProtocolSupported.Value
        )
        {
            // We don't think the current terminal supports the iTerm2 graphics protocol
            trackMapMessage = $"""
                It seems the current terminal may not support inline graphics, which means we can't show the driver tracker.
                If you think this is incorrect, please open an issue at https://github.com/JustAman62/undercut-f1. 
                Include the diagnostic information below:

                LC_TERMINAL: {Environment.GetEnvironmentVariable("LC_TERMINAL")}
                TERM: {Environment.GetEnvironmentVariable("TERM")}
                TERM_PROGRAM: {Environment.GetEnvironmentVariable("TERM_PROGRAM")}
                """;
        }
        var driverTower = GetDriverTower();
        var statusPanel = GetStatusPanel();
        var layout = new Layout("Content").SplitColumns(
            new Layout("Left Tower")
                .SplitRows(
                    new Layout("Driver List", driverTower),
                    new Layout("Status", statusPanel).Size(6)
                )
                .Size(LEFT_OFFSET - 1),
            new Layout("Track Map", new Text(trackMapMessage)) // Drawn over manually in PostContentDrawAsync()
        );

        _trackMapControlSequence = GetTrackMap();

        return Task.FromResult<IRenderable>(layout);
    }

    private Table GetDriverTower()
    {
        var table = new Table();
        table
            .AddColumns(
                new TableColumn("Drivers") { Width = 8 },
                new TableColumn("Gap") { Width = 7, Alignment = Justify.Right }
            )
            .NoBorder()
            .NoSafeBorder()
            .RemoveColumnPadding();

        var comparisonDataPoint = timingData.Latest.Lines.FirstOrDefault(x =>
            x.Value.Line == state.CursorOffset
        );

        foreach (var (driverNumber, line) in timingData.Latest.GetOrderedLines())
        {
            var driver = driverList.Latest?.GetValueOrDefault(driverNumber) ?? new();
            var car = carData.Latest.Entries.FirstOrDefault()?.Cars.GetValueOrDefault(driverNumber);
            var isComparisonLine = line == comparisonDataPoint.Value;

            var driverTag = DisplayUtils.MarkedUpDriverNumber(driver);
            var decoration = Decoration.None;
            if (!state.SelectedDrivers.Contains(driverNumber))
            {
                driverTag = $"[dim]{driverTag}[/]";
                decoration |= Decoration.Dim;
            }

            driverTag = state.CursorOffset == line.Line ? $">{driverTag}<" : $" {driverTag} ";

            if (sessionInfo.Latest.IsRace())
            {
                table.AddRow(
                    new Markup(driverTag),
                    state.CursorOffset > 0
                        ? DisplayUtils.GetGapBetweenLines(
                            comparisonDataPoint.Value,
                            line,
                            decoration
                        )
                        : new Text(
                            $"{(car?.Channels.Drs >= 8 ? "•" : "")}{line.IntervalToPositionAhead?.Value}".ToFixedWidth(
                                7
                            ),
                            DisplayUtils.GetStyle(
                                line.IntervalToPositionAhead,
                                false,
                                car,
                                decoration
                            )
                        )
                );
            }
            else
            {
                var bestDriver = timingData.Latest.GetOrderedLines().First();
                var position =
                    positionData
                        .Latest.Position.LastOrDefault()
                        ?.Entries.GetValueOrDefault(driverNumber) ?? new();
                var gapToLeader = (
                    line.BestLapTime.ToTimeSpan() - bestDriver.Value.BestLapTime.ToTimeSpan()
                )?.TotalSeconds;

                table.AddRow(
                    new Markup(driverTag),
                    position.Status == PositionDataPoint.PositionData.Entry.DriverStatus.OffTrack
                        ? new Text(
                            "OFF TRK",
                            new Style(background: Color.Red, foreground: Color.White)
                        )
                        : new Text(
                            $"{(gapToLeader > 0 ? "+" : "")}{gapToLeader:f3}".ToFixedWidth(7),
                            DisplayUtils.STYLE_NORMAL.Combine(new Style(decoration: decoration))
                        )
                );
            }
        }

        return table;
    }

    private Panel GetStatusPanel()
    {
        var items = new List<IRenderable>();

        if (trackStatus.Latest is not null)
        {
            var style = trackStatus.Latest.Status switch
            {
                "1" => DisplayUtils.STYLE_PB, // All Clear
                "2" => new Style(foreground: Color.Black, background: Color.Yellow), // Yellow Flag
                "4" => new Style(foreground: Color.Black, background: Color.Yellow), // Safety Car
                "6" => new Style(foreground: Color.Black, background: Color.Yellow), // VSC Deployed
                "5" => new Style(foreground: Color.White, background: Color.Red), // Red Flag
                _ => Style.Plain,
            };
            items.Add(new Text($"{trackStatus.Latest.Message}", style));
        }

        items.Add(new Text($@"{dateTimeProvider.Utc:HH\:mm\:ss}"));
        items.Add(new Text($@"{extrapolatedClock.ExtrapolatedRemaining():hh\:mm\:ss}"));

        var rows = new Rows(items);
        return new Panel(rows)
        {
            Header = new PanelHeader("Status"),
            Expand = true,
            Border = BoxBorder.Rounded,
        };
    }

    private string GetTrackMap()
    {
        if (
            !(
                terminalInfo.IsITerm2ProtocolSupported.Value
                || terminalInfo.IsKittyProtocolSupported.Value
            )
            || sessionInfo.Latest.CircuitPoints.Count == 0
        )
        {
            return string.Empty;
        }

        _transform ??= GetTransformFactors();

        // Draw the image as a square that fits the actual track map in
        var longestEdgeLength = Math.Max(_transform.MaxX, _transform.MaxY);
        var surface = SKSurface.Create(new SKImageInfo(longestEdgeLength, longestEdgeLength));
        var canvas = surface.Canvas;

        var circuitPoints = sessionInfo.Latest.CircuitPoints.Select(x =>
            TransformPoint(x, _transform)
        );
        // Draw lines between all the points of the track to create the track map
        _ = circuitPoints.Aggregate(
            (a, b) =>
            {
                canvas.DrawLine(a.x, a.y, b.x, b.y, _trackLinePaint);
                return b;
            }
        );

        var circuitCorners = sessionInfo.Latest.CircuitCorners.Select(p =>
        {
            var (x, y) = TransformPoint(((int)p.x, (int)p.y), _transform);
            return (p.number, x, y);
        });

        foreach (var (number, x, y) in circuitCorners)
        {
            // Draw the text to the right of the corner
            canvas.DrawText(number.ToString(), x + 10, y, _cornerTextPaint);
        }

        // Add all the selected drivers positions to the map
        foreach (var (driverNumber, data) in driverList.Latest)
        {
            var position = positionData
                .Latest.Position.LastOrDefault()
                ?.Entries.GetValueOrDefault(driverNumber);
            if (position is not null && position.X.HasValue && position.Y.HasValue)
            {
                if (state.SelectedDrivers.Contains(driverNumber))
                {
                    var (x, y) = TransformPoint(
                        (x: position.X.Value, y: position.Y.Value),
                        _transform
                    );
                    var paint = new SKPaint
                    {
                        Color = SKColor.Parse(data.TeamColour),
                        TextSize = 14,
                        Typeface = _boldTypeface,
                    };

                    // Draw a white box around the driver currently selected by the cursor
                    if (timingData.Latest.Lines[driverNumber].Line == state.CursorOffset)
                    {
                        var rectPaint = new SKPaint { Color = SKColor.Parse("FFFFFF") };
                        canvas.DrawRoundRect(x - 6, y - 8, 46, 16, 4, 4, rectPaint);
                    }

                    canvas.DrawCircle(x, y, 5, paint);
                    canvas.DrawText(data.Tla, x + 8, y + 6, paint);
                }
            }
        }

        var windowHeight = Terminal.Size.Height - TOP_OFFSET - BOTTOM_OFFSET;
        var windowWidth = Terminal.Size.Width - LEFT_OFFSET;
        // Terminal protocols will distort the image, so provide height/width as the biggest square that will definitely fit
        // Terminal cells are ~twice as high as they are wide, so take that in to consideration
        var shortestWindowEdgeLength = Math.Min(windowWidth, windowHeight * 2);
        windowHeight = shortestWindowEdgeLength / 2;
        windowWidth = shortestWindowEdgeLength;

        if (options.Value.Verbose)
        {
            // Add some debug information when verbose mode is on
            canvas.DrawRect(0, 0, longestEdgeLength - 1, longestEdgeLength - 1, _errorPaint);
            canvas.DrawText(
                $"iTerm2 Support: {terminalInfo.IsITerm2ProtocolSupported.Value}",
                5,
                20,
                _errorPaint
            );
            canvas.DrawText(
                $"Kitty Support: {terminalInfo.IsKittyProtocolSupported.Value}",
                5,
                40,
                _errorPaint
            );
            canvas.DrawText(
                $"Window H/W: {windowHeight}/{windowWidth} Shortest: {shortestWindowEdgeLength}",
                5,
                60,
                _errorPaint
            );
            canvas.DrawText(
                $"Synchronized Output Support: {terminalInfo.IsSynchronizedOutputSupported}",
                5,
                80,
                _errorPaint
            );
            canvas.DrawText($"Image Scale factor: {_transform.ScaleFactor}", 5, 100, _errorPaint);
            canvas.DrawText($"Tranforms: {_transform}", 5, 120, _errorPaint);
        }

        var imageData = surface.Snapshot().Encode();
        var base64 = Convert.ToBase64String(imageData.AsSpan());

        if (terminalInfo.IsKittyProtocolSupported.Value)
        {
            return TerminalGraphics.KittyGraphicsSequenceDelete()
                + TerminalGraphics.KittyGraphicsSequence(windowHeight, windowWidth, base64);
        }
        else if (terminalInfo.IsITerm2ProtocolSupported.Value)
        {
            return TerminalGraphics.ITerm2GraphicsSequence(windowHeight, windowWidth, base64);
        }

        return "Unexpected error, shouldn't have got here. Please report!";
    }

    private TransformFactors GetTransformFactors()
    {
        var circuitPoints = sessionInfo.Latest.CircuitPoints;

        // Shift all points in to positive coordinates
        var minX = circuitPoints.Min(x => x.x);
        var minY = circuitPoints.Min(x => x.y);

        circuitPoints = circuitPoints.Select(p => (x: p.x - minX, y: p.y - minY)).ToList();

        var maxX = circuitPoints.Max(x => x.x);
        var maxY = circuitPoints.Max(x => x.y);
        var imageScaleFactor = Math.Max(maxX / TARGET_IMAGE_SIZE, maxY / TARGET_IMAGE_SIZE);

        return new(
            ScaleFactor: imageScaleFactor,
            ShiftX: minX - IMAGE_PADDING,
            ShiftY: minY - IMAGE_PADDING,
            MaxX: (maxX / imageScaleFactor) + (IMAGE_PADDING * 2),
            MaxY: (maxY / imageScaleFactor) + (IMAGE_PADDING * 2)
        );
    }

    private (int x, int y) TransformPoint((int x, int y) point, TransformFactors transform)
    {
        var (x, y) = point;
        x = ((x - transform.ShiftX) / transform.ScaleFactor) + IMAGE_PADDING;
        // Invert the y to account for map coordinate vs image coordinate difference
        y = transform.MaxY - ((y - transform.ShiftY) / transform.ScaleFactor) - IMAGE_PADDING;
        return (x, y);
    }

    /// <inheritdoc />
    public async Task PostContentDrawAsync()
    {
        await Terminal.OutAsync(ControlSequences.MoveCursorTo(TOP_OFFSET, LEFT_OFFSET));
        await Terminal.OutAsync(_trackMapControlSequence);
    }

    private record TransformFactors(int ScaleFactor, int ShiftX, int ShiftY, int MaxX, int MaxY);
}
