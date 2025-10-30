namespace EventBookingPlatform.DTOs
{
    public class BookingEmailDto
    {
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int Seats { get; set; }
    }
}
