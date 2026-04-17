namespace GoEventFunctionApp.Models
{
    public class PaymentEmailDto
    {
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public int Seats { get; set; }
        public DateTime PaidAt { get; set; }
    }
}
