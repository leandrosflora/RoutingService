namespace RoutingService.Domain;

public sealed class LaneSchedule
{
    public Guid Id { get; private set; }
    public Guid LogisticsLaneId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly DepartureTime { get; private set; }
    public bool IsActive { get; private set; }

    private LaneSchedule()
    {
    }

    public LaneSchedule(DayOfWeek dayOfWeek, TimeOnly departureTime, bool isActive = true)
    {
        Id = Guid.NewGuid();
        DayOfWeek = dayOfWeek;
        DepartureTime = departureTime;
        IsActive = isActive;
    }
}
