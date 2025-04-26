using AutoMapper;

namespace UndercutF1.Data.AutoMapper;

public class PitStopSeriesDataPointConfiguration : Profile
{
    public PitStopSeriesDataPointConfiguration()
    {
        CreateMap<PitStopSeriesDataPoint, PitStopSeriesDataPoint>()
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));

        CreateMap<PitStopSeriesDataPoint.PitTime, PitStopSeriesDataPoint.PitTime>()
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));

        CreateMap<
            PitStopSeriesDataPoint.PitTime.PitStopEntry,
            PitStopSeriesDataPoint.PitTime.PitStopEntry
        >()
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));

        CreateMap<
            Dictionary<string, PitStopSeriesDataPoint.PitTime>,
            Dictionary<string, PitStopSeriesDataPoint.PitTime>
        >()
            .ConvertUsingDictionaryMerge();

        CreateMap<
            Dictionary<string, Dictionary<string, PitStopSeriesDataPoint.PitTime>>,
            Dictionary<string, Dictionary<string, PitStopSeriesDataPoint.PitTime>>
        >()
            .ConvertUsingDictionaryMerge();
    }
}
