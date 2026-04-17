using System.ComponentModel.DataAnnotations;

namespace EventBookingPlatform.DTOs
{
    public class CreateCheckoutSessionDto
    {
        [Required] public string BookingId { get; set; } = "";
        [Required] public string EventId { get; set; } = "";
        [Required] public string EventName { get; set; } = "";
        [Range(1, int.MaxValue)] public int Seats { get; set; }
        [Range(0, double.MaxValue)] public decimal PricePerSeat { get; set; }
    }
}
