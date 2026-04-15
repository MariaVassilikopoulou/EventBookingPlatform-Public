using System.ComponentModel.DataAnnotations;

namespace EventBookingPlatform.DTOs
{
    public class CreateBookingDto
    {
        [Required]
        public string EventId { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Seats must be at least 1.")]
        public int Seats { get; set; }

        [Required]
        public string EventName { get; set; } = string.Empty;
    }
}
