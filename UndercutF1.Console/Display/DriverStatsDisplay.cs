using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Data;

namespace UndercutF1.Console.Display;

public class DriverStatsDisplay(
    State state,
    CommonDisplayComponents common,
    TimingDataProcessor timingData,
    DriverListProcessor driverList,
    SessionInfoProcessor sessionInfoProcessor,
    IDateTimeProvider dateTimeProvider
) : IDisplay
{
    public Screen Screen => Screen.DriverStats;

    public Task<IRenderable> GetContentAsync()
    {
        var statusPanel = common.GetStatusPanel();
        var statsTable = CreateStatsTable();

        var layout = new Layout("Root").SplitRows(
            new Layout("Stats", statsTable),
            new Layout("Status", statusPanel).Size(6)
        );

        return Task.FromResult<IRenderable>(layout);
    }

    private IRenderable CreateStatsTable()
    {
        if (timingData.Latest is null)
            return new Text("No Timing Available");

        var table = new Table()
            .AddColumns(
                new TableColumn("Driver") { Width = 10 },
                new TableColumn("Last Lap") { Alignment = Justify.Right },
                new TableColumn("Gap (s)") { Alignment = Justify.Right },
                new TableColumn("Gap (%)") { Alignment = Justify.Right },
                new TableColumn("Avg Gap (s)") { Alignment = Justify.Right },
                new TableColumn("Avg Gap (%)") { Alignment = Justify.Right },
                new TableColumn("Best Lap") { Alignment = Justify.Right },
                new TableColumn("Gap (s)") { Alignment = Justify.Right },
                new TableColumn("Gap (%)") { Alignment = Justify.Right }
            )
            .Border(TableBorder.Rounded)
            .Title("Driver Statistics");

        var lines = timingData.Latest.GetOrderedLines();
        
        // Find the leader (P1)
        var leaderLine = lines.Values.FirstOrDefault(x => x.Line == 1);
        var leaderDriverNumber = lines.FirstOrDefault(x => x.Value == leaderLine).Key;
        var leaderLastLap = leaderLine?.LastLapTime?.ToTimeSpan();
        var leaderBestLap = leaderLine?.BestLapTime?.ToTimeSpan();

        foreach (var (driverNumber, line) in lines)
        {
            var driver = driverList.Latest?.GetValueOrDefault(driverNumber) ?? new();

            var driverLastLap = line.LastLapTime?.ToTimeSpan();
            var driverBestLap = line.BestLapTime?.ToTimeSpan();

            // Calculate Last Lap Deltas
            string lastLapGapS = "-";
            string lastLapGapPct = "-";
            
            if (driverLastLap.HasValue && leaderLastLap.HasValue)
            {
                var diff = driverLastLap.Value - leaderLastLap.Value;
                lastLapGapS = FormatDelta(diff);
                
                if (leaderLastLap.Value.TotalSeconds > 0)
                {
                    var pct = (diff.TotalSeconds / leaderLastLap.Value.TotalSeconds) * 100;
                    lastLapGapPct = $"{pct:F2}%";
                }
            }

            // Calculate Average Gap
            string avgGapS = "-";
            string avgGapPct = "-";

            if (leaderDriverNumber != null)
            {
                var (avgSeconds, avgPercent) = CalculateAverageGap(driverNumber, leaderDriverNumber);
                if (avgSeconds.HasValue)
                {
                    avgGapS = FormatDelta(TimeSpan.FromSeconds(avgSeconds.Value));
                }
                if (avgPercent.HasValue)
                {
                    avgGapPct = $"{avgPercent.Value:F2}%";
                }
            }

            // Calculate Best Lap Deltas
            string bestLapGapS = "-";
            string bestLapGapPct = "-";

            if (driverBestLap.HasValue && leaderBestLap.HasValue)
            {
                var diff = driverBestLap.Value - leaderBestLap.Value;
                bestLapGapS = FormatDelta(diff);

                if (leaderBestLap.Value.TotalSeconds > 0)
                {
                    var pct = (diff.TotalSeconds / leaderBestLap.Value.TotalSeconds) * 100;
                    bestLapGapPct = $"{pct:F2}%";
                }
            }

            table.AddRow(
                DisplayUtils.DriverTag(driver, line, selected: false),
                new Text(line.LastLapTime?.Value ?? "-", DisplayUtils.STYLE_NORMAL),
                new Text(lastLapGapS, GetDeltaStyle(lastLapGapS)),
                new Text(lastLapGapPct, GetDeltaStyle(lastLapGapS)),
                new Text(avgGapS, GetDeltaStyle(avgGapS)),
                new Text(avgGapPct, GetDeltaStyle(avgGapS)),
                new Text(line.BestLapTime?.Value ?? "-", DisplayUtils.STYLE_NORMAL),
                new Text(bestLapGapS, GetDeltaStyle(bestLapGapS)),
                new Text(bestLapGapPct, GetDeltaStyle(bestLapGapS))
            );
        }

        return table;
    }

    private (double? AvgSeconds, double? AvgPercent) CalculateAverageGap(string driverNumber, string leaderDriverNumber)
    {
        if (driverNumber == leaderDriverNumber) return (0, 0);

        var totalGapSeconds = 0.0;
        var totalGapPercent = 0.0;
        var count = 0;

        foreach (var lap in timingData.DriversByLap)
        {
            var driversOnLap = lap.Value;
            if (!driversOnLap.TryGetValue(driverNumber, out var driverData) || 
                !driversOnLap.TryGetValue(leaderDriverNumber, out var leaderData))
            {
                continue;
            }

            var driverTime = driverData.LastLapTime?.ToTimeSpan();
            var leaderTime = leaderData.LastLapTime?.ToTimeSpan();

            if (driverTime.HasValue && leaderTime.HasValue && leaderTime.Value.TotalSeconds > 0)
            {
                var diff = driverTime.Value - leaderTime.Value;
                totalGapSeconds += diff.TotalSeconds;
                totalGapPercent += (diff.TotalSeconds / leaderTime.Value.TotalSeconds) * 100;
                count++;
            }
        }

        if (count == 0) return (null, null);

        return (totalGapSeconds / count, totalGapPercent / count);
    }

    private string FormatDelta(TimeSpan diff)
    {
        var sign = diff >= TimeSpan.Zero ? "+" : "-";
        return $"{sign}{diff.Duration().TotalSeconds:F3}";
    }

    private Style GetDeltaStyle(string deltaString)
    {
        if (deltaString == "-") return DisplayUtils.STYLE_NORMAL;
        if (deltaString.StartsWith("-")) return new Style(Color.Green);
        if (deltaString.StartsWith("+")) return new Style(Color.Red);
        return DisplayUtils.STYLE_NORMAL;
    }
}
