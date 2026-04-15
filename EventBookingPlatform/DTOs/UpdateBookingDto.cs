using System.ComponentModel.DataAnnotations;

namespace EventBookingPlatform.DTOs
{
    public class UpdateBookingDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Seats must be at least 1.")]
        public int Seats { get; set; }
    }
}
