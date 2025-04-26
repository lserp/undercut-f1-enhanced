namespace UndercutF1.Data;

public sealed record PitStopSeriesDataPoint : ILiveTimingDataPoint
{
    /// <inheritdoc />
    public LiveTimingDataType LiveTimingDataType => LiveTimingDataType.PitStopSeries;

    public Dictionary<string, Dictionary<string, PitTime>> PitTimes { get; set; } = [];

    public sealed record PitTime
    {
        public DateTime? Timestamp { get; set; }
        public PitStopEntry? PitStop { get; set; }

        public sealed record PitStopEntry
        {
            public string? RacingNumber { get; set; }
            public string? PitStopTime { get; set; }
            public string? PitLaneTime { get; set; }
            public string? Lap { get; set; }
        }
    }
}
