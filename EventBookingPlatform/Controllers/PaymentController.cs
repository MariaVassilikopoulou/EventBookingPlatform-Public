using EventBookingPlatform.AzureServices;
using EventBookingPlatform.DTOs;
using EventBookingPlatform.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace EventBookingPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IBookingService _bookingService;
        private readonly ServiceBusService _serviceBusService;

        public PaymentController(IConfiguration config, IBookingService bookingService, ServiceBusService serviceBusService)
        {
            _config = config;
            _bookingService = bookingService;
            _serviceBusService = serviceBusService;
        }

        [HttpPost("create-checkout-session")]
        [Authorize]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionDto dto)
        {
            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];

            var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:3000";

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "sek",
                            UnitAmount = (long)(dto.PricePerSeat * 100),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"{dto.EventName} — {dto.Seats} seat(s)"
                            }
                        },
                        Quantity = dto.Seats
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    { "bookingId", dto.BookingId },
                    { "eventId", dto.EventId }
                },
                SuccessUrl = $"{frontendUrl}/payment-success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{frontendUrl}/bookings"
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return Ok(new { url = session.Url });
        }

        [HttpPost("verify-session")]
        [Authorize]
        public async Task<IActionResult> VerifySession([FromBody] VerifySessionDto dto)
        {
            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];

            Session session;
            try
            {
                var service = new SessionService();
                session = await service.GetAsync(dto.SessionId);
            }
            catch (StripeException)
            {
                return BadRequest("Invalid session.");
            }

            if (session.PaymentStatus != "paid")
                return Ok(new { status = session.PaymentStatus });

            if (session.Metadata == null ||
                !session.Metadata.TryGetValue("bookingId", out var bookingId) ||
                !session.Metadata.TryGetValue("eventId", out var eventId))
                return BadRequest("Session metadata missing.");

            var existing = await _bookingService.GetBookingByIdAsync(bookingId, eventId);
            if (existing == null)
                return NotFound("Booking not found.");

            // Already processed — return without sending another email
            if (existing.Status == "Paid")
                return Ok(new { status = "Paid", bookingId });

            var booking = await _bookingService.UpdateBookingStatusAsync(bookingId, eventId, "Paid", session.Id);
            if (booking == null)
                return StatusCode(500, "Failed to update booking.");

            try
            {
                var paymentQueueName = _config["ServiceBus:PaymentQueueName"];
                await _serviceBusService.SendMessageAsync(new
                {
                    booking.UserEmail,
                    booking.UserName,
                    booking.EventName,
                    BookingId = booking.Id,
                    booking.Seats,
                    PaidAt = DateTime.UtcNow
                }, paymentQueueName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to queue payment email: {ex.Message}");
            }

            return Ok(new { status = "Paid", bookingId });
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _config["Stripe:WebhookSecret"]
                );

                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session?.Metadata != null &&
                        session.Metadata.TryGetValue("bookingId", out var bookingId) &&
                        session.Metadata.TryGetValue("eventId", out var eventId))
                    {
                        var booking = await _bookingService.UpdateBookingStatusAsync(bookingId, eventId, "Paid", session.Id);
                        if (booking != null)
                        {
                            try
                            {
                                var paymentQueueName = _config["ServiceBus:PaymentQueueName"];
                                await _serviceBusService.SendMessageAsync(new
                                {
                                    booking.UserEmail,
                                    booking.UserName,
                                    booking.EventName,
                                    BookingId = booking.Id,
                                    booking.Seats,
                                    PaidAt = DateTime.UtcNow
                                }, paymentQueueName);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to queue payment email: {ex.Message}");
                            }
                        }
                    }
                }

                return Ok();
            }
            catch (StripeException)
            {
                return BadRequest();
            }
        }
    }
}
