using AutoMapper;

namespace UndercutF1.Data;

public class DriverListProcessor(IMapper mapper) : ProcessorBase<DriverListDataPoint>(mapper)
{
    public bool IsSelected(string driverNumber) =>
        Latest.GetValueOrDefault(driverNumber)?.IsSelected ?? true;
}
