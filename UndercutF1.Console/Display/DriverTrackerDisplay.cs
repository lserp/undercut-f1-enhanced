using Microsoft.Extensions.Options;
using SkiaSharp;
using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Console.Graphics;
using UndercutF1.Data;

namespace UndercutF1.Console;

public class DriverTrackerDisplay : IDisplay
{
    private const int IMAGE_PADDING = 70;
    private const int LEFT_OFFSET = 17;
    private const int TOP_OFFSET = 0;
    private const int BOTTOM_OFFSET = 1;

    private static readonly SKPaint _trackLinePaint = new()
    {
        Color = SKColor.Parse("666666"),
        StrokeWidth = 6,
        IsAntialias = false,
    };
    private static readonly SKPaint _cornerTextPaint = new()
    {
        Color = SKColor.Parse("DDDDDD"),
        TextSize = 24,
        Typeface = SKTypeface.FromFamilyName(
            "Consolas",
            weight: SKFontStyleWeight.SemiBold,
            width: SKFontStyleWidth.Normal,
            slant: SKFontStyleSlant.Upright
        ),
        IsAntialias = false,
    };
    private static readonly SKPaint _selectedPaint = new()
    {
        Color = SKColor.Parse("FFFFFF"),
        IsAntialias = false,
    };
    private static readonly SKPaint _errorPaint = new()
    {
        Color = SKColor.Parse("FF0000"),
        IsStroke = true,
        Typeface = _boldTypeface,
        IsAntialias = false,
    };
    private static readonly SKTypeface _boldTypeface = SKTypeface.FromFamilyName(
        "Consolas",
        weight: SKFontStyleWeight.ExtraBold,
        width: SKFontStyleWidth.Normal,
        slant: SKFontStyleSlant.Upright
    );

    private readonly State _state;
    private readonly CommonDisplayComponents _common;
    private readonly TimingDataProcessor _timingData;
    private readonly DriverListProcessor _driverList;
    private readonly PositionDataProcessor _positionData;
    private readonly CarDataProcessor _carData;
    private readonly SessionInfoProcessor _sessionInfo;
    private readonly TerminalInfoProvider _terminalInfo;
    private readonly IOptions<Options> _options;
    private TransformFactors? _transform = null;

    private string[] _trackMapControlSequence = [];

    public DriverTrackerDisplay(
        State state,
        CommonDisplayComponents common,
        TimingDataProcessor timingData,
        DriverListProcessor driverList,
        PositionDataProcessor positionData,
        CarDataProcessor carData,
        SessionInfoProcessor sessionInfo,
        TerminalInfoProvider terminalInfo,
        IOptions<Options> options
    )
    {
        _state = state;
        _common = common;
        _timingData = timingData;
        _driverList = driverList;
        _positionData = positionData;
        _carData = carData;
        _sessionInfo = sessionInfo;
        _terminalInfo = terminalInfo;
        _options = options;

        // The transform factors are dependant on the size of the terminal, so force a recalc if the size changes
        Terminal.Resized += (_size) => _transform = null;
    }

    public Screen Screen => Screen.DriverTracker;

    public Task<IRenderable> GetContentAsync()
    {
        var trackMapMessage = string.Empty;
        if (
            !_terminalInfo.IsITerm2ProtocolSupported.Value
            && !_terminalInfo.IsKittyProtocolSupported.Value
            && !_terminalInfo.IsSixelSupported.Value
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
        var statusPanel = _common.GetStatusPanel();
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

        var comparisonDataPoint = _timingData.Latest.Lines.FirstOrDefault(x =>
            x.Value.Line == _state.CursorOffset
        );

        foreach (var (driverNumber, line) in _timingData.Latest.GetOrderedLines())
        {
            var driver = _driverList.Latest?.GetValueOrDefault(driverNumber) ?? new();
            var car = _carData
                .Latest.Entries.FirstOrDefault()
                ?.Cars.GetValueOrDefault(driverNumber);
            var isComparisonLine = line == comparisonDataPoint.Value;

            var driverTag = DisplayUtils.MarkedUpDriverNumber(driver);
            var decoration = Decoration.None;
            if (!driver.IsSelected)
            {
                driverTag = $"[dim]{driverTag}[/]";
                decoration |= Decoration.Dim;
            }

            driverTag = _state.CursorOffset == line.Line ? $">{driverTag}<" : $" {driverTag} ";

            if (_sessionInfo.Latest.IsRace())
            {
                table.AddRow(
                    new Markup(driverTag),
                    _state.CursorOffset > 0
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
                var bestDriver = _timingData.Latest.GetOrderedLines().First();
                var position =
                    _positionData
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

    private string[] GetTrackMap()
    {
        if (
            !(
                _terminalInfo.IsITerm2ProtocolSupported.Value
                || _terminalInfo.IsKittyProtocolSupported.Value
                || _terminalInfo.IsSixelSupported.Value
            )
            || _sessionInfo.Latest.CircuitPoints.Count == 0
        )
        {
            return [];
        }

        _transform ??= GetTransformFactors();

        var surface = SKSurface.Create(
            new SKImageInfo(_transform.MaxX, _transform.MaxY, SKColorType.Argb4444)
        );
        var canvas = surface.Canvas;

        var circuitPoints = _sessionInfo.Latest.CircuitPoints.Select(x =>
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

        var circuitCorners = _sessionInfo.Latest.CircuitCorners.Select(p =>
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
        foreach (var (driverNumber, driver) in _driverList.Latest)
        {
            var position = _positionData
                .Latest.Position.LastOrDefault()
                ?.Entries.GetValueOrDefault(driverNumber);
            if (position is not null && position.X.HasValue && position.Y.HasValue)
            {
                var driverHighlighted =
                    _timingData.Latest.Lines[driverNumber].Line == _state.CursorOffset;
                if (driver.IsSelected || driverHighlighted)
                {
                    var (x, y) = TransformPoint(
                        (x: position.X.Value, y: position.Y.Value),
                        _transform
                    );
                    var paint = new SKPaint
                    {
                        Color = SKColor.Parse(driver.TeamColour),
                        TextSize = 24,
                        Typeface = _boldTypeface,
                        IsAntialias = false,
                    };

                    // Draw a white box around the driver currently selected by the cursor
                    if (driverHighlighted)
                    {
                        var rectPaint = _selectedPaint;
                        canvas.DrawRoundRect(x - 8, y - 12, 65, 24, 4, 4, rectPaint);
                    }

                    canvas.DrawCircle(x, y, 5, paint);
                    canvas.DrawText(driver.Tla, x + 8, y + 8, paint);
                }
            }
        }

        var windowHeight = Terminal.Size.Height - TOP_OFFSET - BOTTOM_OFFSET;
        var windowWidth = Terminal.Size.Width - LEFT_OFFSET;

        var targetAspectRatio =
            Convert.ToDouble(_transform.MaxX) / Convert.ToDouble(_transform.MaxY);
        // Terminal cells are ~twice as high as they are wide, so take that in to consideration
        var availableAspectRatio = windowWidth / (windowHeight * 2.2);

        var cellAspectRatio = 1 / _terminalInfo.TerminalSize.Value.CellAspectRatio;

        if (targetAspectRatio > availableAspectRatio)
        {
            windowHeight = (int)Math.Ceiling(windowWidth / targetAspectRatio / 2.2);
        }
        else
        {
            windowWidth = (int)Math.Ceiling(windowHeight * 2.2 * targetAspectRatio);
        }

        if (_options.Value.Verbose)
        {
            // Add some debug information when verbose mode is on
            canvas.DrawRect(0, 0, _transform.MaxX - 1, _transform.MaxY - 1, _errorPaint);
            canvas.DrawText(
                $"iTerm2 Support: {_terminalInfo.IsITerm2ProtocolSupported.Value}",
                5,
                20,
                _errorPaint
            );
            canvas.DrawText(
                $"Kitty Support: {_terminalInfo.IsKittyProtocolSupported.Value}",
                5,
                40,
                _errorPaint
            );
            canvas.DrawText(
                $"Window H/W: {windowHeight}/{windowWidth} Target/Avail: {targetAspectRatio:F2}/{availableAspectRatio:F2}",
                5,
                60,
                _errorPaint
            );
            canvas.DrawText(
                $"Synchronized Output Support: {_terminalInfo.IsSynchronizedOutputSupported}",
                5,
                80,
                _errorPaint
            );
            canvas.DrawText($"Image Scale factor: {_transform.ScaleFactor}", 5, 100, _errorPaint);
            canvas.DrawText($"Transforms: {_transform}", 5, 120, _errorPaint);
        }

        var image = surface.Snapshot();

        if (_terminalInfo.IsKittyProtocolSupported.Value)
        {
            var imageData = image.Encode();
            var base64 = Convert.ToBase64String(imageData.AsSpan());
            return
            [
                TerminalGraphics.KittyGraphicsSequenceDelete(),
                .. TerminalGraphics.KittyGraphicsSequence(windowHeight, windowWidth, base64),
            ];
        }
        else if (_terminalInfo.IsITerm2ProtocolSupported.Value)
        {
            var imageData = image.Encode();
            var base64 = Convert.ToBase64String(imageData.AsSpan());
            return [TerminalGraphics.ITerm2GraphicsSequence(windowHeight, windowWidth, base64)];
        }
        else if (_terminalInfo.IsSixelSupported.Value)
        {
            var bitmap = SKBitmap.FromImage(image);
            var sixelData = Sixel.ImageToSixel(bitmap.Pixels, bitmap.Width);
            return [TerminalGraphics.SixelGraphicsSequence(sixelData)];
        }

        return ["Unexpected error, shouldn't have got here. Please report!"];
    }

    private TransformFactors GetTransformFactors()
    {
        var circuitPoints = _sessionInfo.Latest.CircuitPoints;
        var terminalSize = _terminalInfo.TerminalSize.Value;

        // Shift all points in to positive coordinates
        var minX = circuitPoints.Min(x => x.x);
        var minY = circuitPoints.Min(x => x.y);

        circuitPoints = circuitPoints.Select(p => (x: p.x - minX, y: p.y - minY)).ToList();

        var maxX = circuitPoints.Max(x => x.x);
        var maxY = circuitPoints.Max(x => x.y);

        var targetAspectRatio = maxX / (double)maxY;

        var availableRows = terminalSize.Rows - TOP_OFFSET - BOTTOM_OFFSET;
        var availableColumns = terminalSize.Columns - LEFT_OFFSET - 2;
        var availableAspectRatio =
            terminalSize.ColumnsToPixels(availableColumns)
            / (double)terminalSize.RowsToPixels(availableRows);

        var imageScaleFactor =
            availableAspectRatio > targetAspectRatio
                ? maxY / (terminalSize.RowsToPixels(availableRows) - (IMAGE_PADDING * 3))
                : maxX / (terminalSize.ColumnsToPixels(availableColumns) - (IMAGE_PADDING * 3));

        // Add one to ensure we always have a smaller image than we need
        imageScaleFactor += 1;

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
        foreach (var sequence in _trackMapControlSequence)
        {
            await Terminal.OutAsync(sequence);
        }
    }

    private record TransformFactors(int ScaleFactor, int ShiftX, int ShiftY, int MaxX, int MaxY);
}
