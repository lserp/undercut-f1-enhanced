using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Data;

namespace UndercutF1.Console;

public class TyreStintDisplay(
    State state,
    CommonDisplayComponents common,
    TyreStintSeriesProcessor tyreStintSeries,
    PitStopSeriesProcessor pitStopSeries,
    DriverListProcessor driverList,
    TimingDataProcessor timingData,
    LapCountProcessor lapCount
) : IDisplay
{
    public Screen Screen => Screen.TyreStints;

    public Task<IRenderable> GetContentAsync()
    {
        var pitStintList = GetPitStintList();

        var layout = new Layout("Root").SplitRows(
            new Layout("Pit Stints", pitStintList),
            new Layout("Footer")
                .SplitColumns(
                    new Layout("Status Panel", common.GetStatusPanel()).Size(15),
                    new Layout("Selected Stint Detail", GetStintDetail())
                )
                .Size(6)
        );

        return Task.FromResult<IRenderable>(layout);
    }

    private Rows GetPitStintList()
    {
        var rows = new List<IRenderable>
        {
            new Text(
                $"LAP {lapCount.Latest.CurrentLap, 2}/{lapCount.Latest.TotalLaps, 2} Pit Stops"
            ),
        };
        var totalLapCount = lapCount.Latest.TotalLaps.GetValueOrDefault();

        foreach (var (driverNumber, line) in timingData.Latest.GetOrderedLines())
        {
            var driver = driverList.Latest.GetValueOrDefault(driverNumber) ?? new();
            var stints = tyreStintSeries.Latest.Stints.GetValueOrDefault(driverNumber) ?? [];
            var rowMarkup = DisplayUtils.MarkedUpDriverNumber(driver);
            rowMarkup = $"{line.Line.ToString()?.ToFixedWidth(2)} {rowMarkup} ";

            var (selectedDriverNumber, _) = timingData.Latest.Lines.FirstOrDefault(x =>
                x.Value.Line == state.CursorOffset
            );

            if (selectedDriverNumber == driverNumber)
            {
                rowMarkup = $"[invert]{rowMarkup}[/]";
            }

            var lineTotalPadLength = 0;

            foreach (var (stintNumber, stint) in stints.OrderBy(x => x.Key))
            {
                var markup = DisplayUtils.GetStyleForTyreCompound(stint.Compound).ToMarkup();
                var lapsOnThisTyre = (stint.TotalLaps - stint.StartLaps).GetValueOrDefault();

                var padLength = Math.Max(1, lapsOnThisTyre - 1);
                var text = $"{lapsOnThisTyre}".ToFixedWidth(padLength);
                if (text.Length == 1)
                {
                    text = string.Empty;
                }
                lineTotalPadLength += text.Length + 1;

                // Prepend the compound indicator, and wrap the whole line in markup to colour it
                rowMarkup += $"[{markup}]{stint.Compound?[0] ?? ' '}{text}[/]";
            }

            if (totalLapCount > 0)
            {
                // Add a white cell for the final lap
                var emptyCellsToAdd = Math.Max(0, totalLapCount - lineTotalPadLength);
                var emptyCells = string.Empty.ToFixedWidth(emptyCellsToAdd);
                rowMarkup = rowMarkup + emptyCells + "[white on white] [/]";
            }

            rows.Add(new Markup(rowMarkup));
        }

        return new Rows(rows);
    }

    private Columns GetStintDetail()
    {
        var (selectedDriverNumber, _) = timingData.Latest.Lines.FirstOrDefault(x =>
            x.Value.Line == state.CursorOffset
        );
        if (selectedDriverNumber is null)
        {
            return new Columns();
        }

        var stints = tyreStintSeries.Latest.Stints.GetValueOrDefault(selectedDriverNumber) ?? [];

        var columns = new List<Rows>();
        foreach (var (stintNumber, stint) in stints)
        {
            var pitStop = pitStopSeries
                .Latest.PitTimes.GetValueOrDefault(selectedDriverNumber)
                ?.GetValueOrDefault((int.Parse(stintNumber) - 1).ToString())
                ?.PitStop;
            var compoundMarkup = DisplayUtils.GetStyleForTyreCompound(stint.Compound).ToMarkup();
            // Use a consistent tyre compound header to centre it nicely
            var header = stint.Compound switch
            {
                "HARD" => "     HARD     ",
                "MEDIUM" => "    MEDIUM    ",
                "SOFT" => "     SOFT     ",
                "INTERMEDIATE" => " INTERMEDIATE ",
                "WET" => "      WET     ",
                _ => "    UNKNOWN   ",
            };
            var rows = new List<Markup>
            {
                new($"[{compoundMarkup}]{header}[/]"),
                new(
                    $"Start Age  {(stint.New.GetValueOrDefault() ? "[green]NEW[/]" : $" {stint.StartLaps:D2}")}"
                ),
                new($"Total Laps  {stint.TotalLaps:D2}"),
                pitStop is null ? new(" ") : new($"Stop Time  {pitStop?.PitStopTime}"),
                pitStop is null ? new(" ") : new($"Lane    {pitStop?.PitLaneTime}"),
            };
            columns.Add(new Rows(rows).Collapse());
        }
        return new Columns(columns).Collapse();
    }
}
