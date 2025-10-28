using AutoMapper;
using EventBookingPlatform.AzureServices;
using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.DTOs;
using EventBookingPlatform.Interfaces;

namespace EventBookingPlatform.Services
{
    public class BookingService : IBookingService
    {
        private readonly IGenericRepository<Booking> _bookingRepository;
        private readonly IGenericRepository<Event> _eventRepository;
        private IMapper _mapper;
        private readonly ServiceBusService _serviceBusService;
        public BookingService(IGenericRepository<Booking> bookingRepository,IGenericRepository<Event> eventRepository,
            IMapper mapper, ServiceBusService serviceBusService)
        {
            _bookingRepository = bookingRepository;
            _eventRepository = eventRepository;
            _mapper = mapper;
            _serviceBusService = serviceBusService;
        }


       public async Task<(bool Success, string Message, Booking? CreatedBooking)> CreateBookingAsync(string userId, string userEmail, string userName, CreateBookingDto bookingDto)
       {
            var ev = await _eventRepository.GetByIdAsync(bookingDto.EventId, bookingDto.EventId);
            if(ev == null)
                return (false, $"Event with ID {bookingDto.EventId} not found", null);
            if (bookingDto.Seats <= 0)
                return (false, "Number of seats must be greater than zero", null);
            if (bookingDto.Seats > ev.AvailableSeats)
                    return(false, $"Only {ev.AvailableSeats} additional seats are availiable", null);
            ev.AvailableSeats -= bookingDto.Seats;
            await _eventRepository.UpdateAsync(ev, ev.PartitionKey);

            var booking = _mapper.Map<Booking>(bookingDto);
            booking.UserId= userId;
            booking.UserEmail= userEmail;
            booking.UserName= userName;

            var created = await _bookingRepository.AddAsync(booking);

            try
            {
                var message = new
                {
                    EventId = bookingDto.EventId,
                    UserId = userId,
                    UserName = userEmail,
                    Seats = bookingDto.Seats,
                    CreatedAt = DateTime.UtcNow
                };
                await _serviceBusService.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message to ServiceBus : {ex.Message}");
            }

            return (true,"Booking created succesfully.", created);

       }


       public async Task<(bool Success, string Message, Booking? UpdatedBooking)> UpdateBookingAsync(string userId, string eventId, string bookingId, UpdateBookingDto bookingDto)
       {
           var existingBooking= await _bookingRepository.GetByIdAsync(bookingId,eventId);
            if(existingBooking == null)
                return (false,"Booking not found", null);
            if(existingBooking.UserId != userId)
                return (false,"You can only update your own booking", null);
            var ev = await _eventRepository.GetByIdAsync(eventId, eventId);
            if (ev == null)
                return (false,$"Event with ID {eventId} not Found", null);

            int oldSeats = existingBooking.Seats;
            int newSeats = bookingDto.Seats;

            if (newSeats <= 0)
                return (false,"Number of Seats must be grater than zero", null);

            int seatDifference = newSeats - oldSeats;
            if (seatDifference > 0)
            {
                if (seatDifference > ev.AvailableSeats)
                    return (false, $"Only {ev.AvailableSeats} additional seats are availiable",null);
                ev.AvailableSeats -= seatDifference;
            }
            else if (seatDifference < 0)
            {
                ev.AvailableSeats += Math.Abs(seatDifference);
            }
            await _eventRepository.UpdateAsync(ev, ev.PartitionKey);

            existingBooking.Seats = newSeats;
            existingBooking.BookingDate = DateTime.UtcNow;
            await _bookingRepository.UpdateAsync(existingBooking, existingBooking.PartitionKey);

            return (true, "Booking updated succesfully", existingBooking);
       }

       public async Task<(bool success, string message)> DeleteBookingAsync(string eventId, string bookingId)
       {
            var success = await _bookingRepository.DeleteAsync(bookingId, eventId);
            return success
                ? (true, "Deleted successfully")
                : (false, "Booking not found");
       }



        public async Task<IEnumerable<Booking>> GetAllBookingsAsync()
        {
            return await _bookingRepository.GetAllAsync();
        }

        public async Task<Booking?> GetBookingByIdAsync( string id, string eventId)
        {
            return await _bookingRepository.GetByIdAsync(id, eventId);
        }

        public async Task<IEnumerable<Booking>> GetBookingsByEventAsync(string eventId)
        {
            return await _bookingRepository.FindAsync(b => b.EventId == eventId, eventId);
        }

        public async Task<IEnumerable<Booking>> GetBookingsByUserAsync(string userId)
        {
            return await _bookingRepository.FindAsync(b => b.UserId == userId, null);
        }



    }
}
