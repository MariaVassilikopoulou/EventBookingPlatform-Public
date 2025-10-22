using AutoMapper;
using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.DTOs;
using EventBookingPlatform.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EventBookingPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IGenericRepository<Booking> _bookingRepository;
            private readonly IGenericRepository<Event> _eventRepository;
            private IMapper _mapper;

            public BookingController(IGenericRepository<Booking> bookingRepository,
                                     IGenericRepository<Event> eventRepository,
                                     IMapper mapper)
            {
                _bookingRepository = bookingRepository;
                _eventRepository = eventRepository;
                _mapper = mapper;
            }

            [HttpPost]
            public async Task<IActionResult> Create([FromBody] CreateBookingDto bookingDto)
            {
                if (bookingDto == null)
                    return BadRequest("Invalid booking data.");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var userName = User.FindFirst("fullName")?.Value ?? "Unknown";
            //var userName = User.Identity?.Name ?? "Unknown";
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User not authenticated.");

            var ev = await _eventRepository.GetByIdAsync(bookingDto.EventId, bookingDto.EventId);
            

                if (ev == null)
                    return NotFound($"Event with ID {bookingDto.EventId} not found.");

                if (bookingDto.Seats <= 0)
                    return BadRequest("Number of seats should be greater than zero");
                if (bookingDto.Seats > ev.AvailableSeats)
                    return BadRequest($"Only {ev.AvailableSeats} seats left for this event");

                ev.AvailableSeats -= bookingDto.Seats;
                await _eventRepository.UpdateAsync(ev, ev.PartitionKey);

                var booking = _mapper.Map<Booking>(bookingDto);
                booking.UserId = userId!;
                booking.UserEmail = userEmail!;
                booking.UserName = userName ;
                var created = await _bookingRepository.AddAsync(booking);

                return CreatedAtAction(nameof(GetById), new { eventId= created.PartitionKey, id = created.Id }, created);
            }

            [HttpGet("{eventId}/{id}")]
            public async Task <IActionResult> GetById(string eventId, string id)
            {
                var booking = await _bookingRepository.GetByIdAsync(id, eventId);
                if(booking == null)
                    return NotFound();
                return Ok(booking);
            }


            [HttpGet]
            public async Task<IActionResult> GetAll()
            {
                var booking = await _bookingRepository.GetAllAsync();
                return Ok(booking);
            }

         
        [HttpDelete("{eventId}/{id}")]
        public async Task <IActionResult> Delete (string eventId, string id)
        {
            var success = await _bookingRepository.DeleteAsync(id, eventId);
            if(!success) return NotFound();
            return NoContent();

        }


        [HttpGet("by-event/{eventId}")]
        public async Task<IActionResult> GetBookingsByEvent(string eventId)
        {
            var bookings = await _bookingRepository.FindAsync(b => b.EventId == eventId, eventId);

            if (!bookings.Any())
                return NotFound($"No bookings found for event ID {eventId}");

            return Ok(bookings);
        }

        [HttpGet("my-bookings")]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var bookings = await _bookingRepository.FindAsync(b => b.UserId == userId, null);
            return Ok(bookings);
        }


    }
}

