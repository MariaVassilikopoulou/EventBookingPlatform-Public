using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.DTOs;

namespace EventBookingPlatform.Interfaces
{
    public interface IBookingService
    {
        Task<(bool Success, string Message, Booking? CreatedBooking)> CreateBookingAsync(string userId, string userEmail, string userName, CreateBookingDto bookingDto);
        Task<(bool Success, string Message, Booking? UpdatedBooking)> UpdateBookingAsync(string userId, string eventId, string bookingId, UpdateBookingDto bookingDto);
        Task<(bool success, string message)> DeleteBookingAsync(string eventId, string id);
        Task<IEnumerable<Booking>> GetAllBookingsAsync();
       
        Task<Booking?> GetBookingByIdAsync( string id, string eventId);
        Task<IEnumerable<Booking>> GetBookingsByEventAsync(string eventId);
        Task<IEnumerable<Booking>> GetBookingsByUserAsync(string userId);
    }
}
