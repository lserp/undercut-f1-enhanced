using AutoMapper;

namespace UndercutF1.Data.AutoMapper;

public class TimingDataPointConfiguration : Profile
{
    public TimingDataPointConfiguration()
    {
        CreateMap<TimingDataPoint, TimingDataPoint>()
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));

        CreateMap<
            Dictionary<string, TimingDataPoint.Driver>,
            Dictionary<string, TimingDataPoint.Driver>
        >()
            .ConvertUsingDictionaryMerge();

        CreateMap<
            Dictionary<string, TimingDataPoint.Driver.LapSectorTime>,
            Dictionary<string, TimingDataPoint.Driver.LapSectorTime>
        >()
            .ConvertUsingDictionaryMerge();
        CreateMap<
            Dictionary<int, TimingDataPoint.Driver.LapSectorTime.Segment>,
            Dictionary<int, TimingDataPoint.Driver.LapSectorTime.Segment>
        >()
            .ConvertUsingDictionaryMerge();

        CreateMap<TimingDataPoint.Driver, TimingDataPoint.Driver>()
            .ForMember(x => x.IsPitLap, x => x.Ignore())
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));

        CreateMap<TimingDataPoint.Driver.BestLap, TimingDataPoint.Driver.BestLap>()
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));

        CreateMap<TimingDataPoint.Driver.LapSectorTime, TimingDataPoint.Driver.LapSectorTime>()
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));

        CreateMap<
            TimingDataPoint.Driver.LapSectorTime.Segment,
            TimingDataPoint.Driver.LapSectorTime.Segment
        >()
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));
    }
}
