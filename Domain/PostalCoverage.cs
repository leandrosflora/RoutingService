namespace RoutingService.Domain;

public sealed class PostalCoverage
{
    public Guid Id { get; private set; }
    public Guid DestinationNodeId { get; private set; }
    public long PostalCodeFrom { get; private set; }
    public long PostalCodeTo { get; private set; }
    public int Priority { get; private set; }

    private PostalCoverage()
    {
    }

    public PostalCoverage(Guid destinationNodeId, long postalCodeFrom, long postalCodeTo, int priority)
    {
        if (destinationNodeId == Guid.Empty)
            throw new ArgumentException("Destination node is required", nameof(destinationNodeId));

        if (postalCodeFrom <= 0 || postalCodeTo <= 0 || postalCodeFrom > postalCodeTo)
            throw new ArgumentException("Postal coverage range is invalid");

        if (priority < 0)
            throw new ArgumentException("Priority cannot be negative", nameof(priority));

        Id = Guid.NewGuid();
        DestinationNodeId = destinationNodeId;
        PostalCodeFrom = postalCodeFrom;
        PostalCodeTo = postalCodeTo;
        Priority = priority;
    }
}
