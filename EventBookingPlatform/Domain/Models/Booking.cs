using EventBookingPlatform.Interfaces;
using Newtonsoft.Json;

namespace EventBookingPlatform.Domain.Models
{
    public class Booking : ICosmosEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string EventId { get; set; }
        public int Seats { get; set; }
        public DateTime BookingDate { get; set; } = DateTime.UtcNow;

        [JsonProperty("partitionKey")]
        public string PartitionKey => EventId;
        public string ContainerName => "Bookings";
    }
}
