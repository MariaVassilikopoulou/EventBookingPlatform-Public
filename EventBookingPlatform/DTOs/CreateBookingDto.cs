namespace EventBookingPlatform.DTOs
{
    public class CreateBookingDto
    {

        //public string UserName { get; set; } = string.Empty;
        //public string UserEmail {  get; set; } = string.Empty;
        public string EventId { get; set; }= string.Empty;
        public int Seats { get; set; }
        public string EventName { get; set; } = string.Empty;

    }
}
