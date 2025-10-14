namespace EventBookingPlatform.Interfaces
{
    public interface ICosmosEntity
    {
        string Id { get; set; }
        string PartitionKey { get; }
        string ContainerName { get; }
    }
}
