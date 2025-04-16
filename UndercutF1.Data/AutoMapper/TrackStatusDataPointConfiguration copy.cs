using AutoMapper;

namespace UndercutF1.Data.AutoMapper;

public class TrackStatusDataPointConfiguration : Profile
{
    public TrackStatusDataPointConfiguration() =>
        CreateMap<TrackStatusDataPoint, TrackStatusDataPoint>()
            .ForAllMembers(opts => opts.Condition((_, _, member) => member != null));
}
