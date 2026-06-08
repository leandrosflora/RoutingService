namespace RoutingService.Domain;

public enum LogisticsNodeType
{
    FulfillmentCenter = 1,
    CrossDocking = 2,
    RegionalHub = 3,
    SortationCenter = 4,
    LastMileStation = 5
}

public enum TransportMode
{
    Road = 1,
    Air = 2,
    Rail = 3,
    InternalTransfer = 4,
    LastMile = 5
}

public enum LaneStatus
{
    Active = 1,
    Suspended = 2,
    Maintenance = 3,
    Inactive = 4
}
