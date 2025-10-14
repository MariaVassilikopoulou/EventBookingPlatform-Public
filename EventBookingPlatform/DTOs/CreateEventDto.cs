namespace EventBookingPlatform.DTOs
{
    public class CreateEventDto
    {
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string Location { get; set; }
        public decimal Price { get; set; }
        public int TotalSeats { get; set; }
    }
}
