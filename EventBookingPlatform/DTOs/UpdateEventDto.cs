namespace EventBookingPlatform.DTOs
{
    public class UpdateEventDto
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public decimal Price { get; set; }
        public int TotalSeats { get; set; }

    }
}
