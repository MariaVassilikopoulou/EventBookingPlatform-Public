using System.ComponentModel.DataAnnotations;

namespace EventBookingPlatform.DTOs
{
    public class UpdateEventDto
    {
        [Required, MinLength(2)]
        public string Name { get; set; }

        [Required, MinLength(2)]
        public string Location { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Price must be zero or greater.")]
        public decimal Price { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Total seats must be at least 1.")]
        public int TotalSeats { get; set; }
    }
}
