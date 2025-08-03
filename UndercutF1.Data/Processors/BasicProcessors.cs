using AutoMapper;

namespace UndercutF1.Data;

public class HeartbeatProcessor(IMapper mapper) : ProcessorBase<HeartbeatDataPoint>(mapper);

public class LapCountProcessor(IMapper mapper) : ProcessorBase<LapCountDataPoint>(mapper);

public class TimingAppDataProcessor(IMapper mapper) : ProcessorBase<TimingAppDataPoint>(mapper);

public class TrackStatusProcessor(IMapper mapper) : ProcessorBase<TrackStatusDataPoint>(mapper);

public class WeatherProcessor(IMapper mapper) : ProcessorBase<WeatherDataPoint>(mapper);

public class ChampionshipPredictionProcessor(IMapper mapper)
    : ProcessorBase<ChampionshipPredictionDataPoint>(mapper);

public class TimingStatsProcessor(IMapper mapper) : ProcessorBase<TimingStatsDataPoint>(mapper);

public class TyreStintSeriesProcessor(IMapper mapper)
    : ProcessorBase<TyreStintSeriesDataPoint>(mapper);

public class PitStopSeriesProcessor(IMapper mapper) : ProcessorBase<PitStopSeriesDataPoint>(mapper);
