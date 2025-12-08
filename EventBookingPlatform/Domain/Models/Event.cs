using EventBookingPlatform.Interfaces;
using Newtonsoft.Json;

namespace EventBookingPlatform.Domain.Models
{
    public class Event: ICosmosEntity
    {
       
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string Location { get; set; }
        public decimal Price { get; set; }
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
       


        [JsonProperty("partitionKey")]
        public string PartitionKey => Id;
        public string ContainerName => "Events";
    }
}
