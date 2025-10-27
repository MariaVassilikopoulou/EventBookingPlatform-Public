using AutoMapper;
using EventBookingPlatform.Domain.Models;
using EventBookingPlatform.DTOs;
using EventBookingPlatform.Interfaces;
using EventBookingPlatform.Services;
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
        private readonly IBookingService _bookingService;


            public BookingController(IBookingService bookingService)
            {
                _bookingService = bookingService;
               
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

            var (success, message, booking) = await _bookingService.CreateBookingAsync(userId, userEmail!, userName, bookingDto);
            if (!success) return BadRequest(message);

            return CreatedAtAction(nameof(GetById), new { eventId = booking!.PartitionKey, id = booking.Id }, booking);
            }

            [HttpGet("{eventId}/{id}")]
            public async Task <IActionResult> GetById(string eventId, string id)
            {
                var booking = await _bookingService.GetBookingByIdAsync(id, eventId);
                if(booking == null)
                    return NotFound();
                return Ok(booking);
            }


            [HttpGet]
            public async Task<IActionResult> GetAll()
            {
                var booking = await _bookingService.GetAllBookingsAsync();
                return Ok(booking);
            }

         
            [HttpDelete("{eventId}/{id}")]
            public async Task <IActionResult> Delete (string eventId, string id)
            {
                var (success, message) = await _bookingService.DeleteBookingAsync(eventId, id);
                if (!success) return NotFound(message);
                return NoContent();

            }


            [HttpGet("by-event/{eventId}")]
            public async Task<IActionResult> GetBookingsByEvent(string eventId)
            {
                var bookings = await _bookingService.GetBookingsByEventAsync(eventId);

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

                var bookings = await _bookingService.GetBookingsByUserAsync(userId);
                return Ok(bookings);
            }

            [HttpPut("{eventId}/{id}")]
            public async Task<IActionResult>Update(string eventId,string id, [FromBody] UpdateBookingDto bookingDto)
            {
            

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not authorized");

                var (success, message, updated) = await _bookingService.UpdateBookingAsync(userId, eventId, id, bookingDto);
                if (!success) return BadRequest(message);

                return Ok(updated);
            }
    }
}

