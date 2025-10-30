using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using EventBookingPlatform.DTOs;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using SendGrid.Helpers.Mail;
using SendGrid;

namespace GoEventFunctionApp.Functions
{
    public class SendBookingEmail
    {
        private readonly ILogger<SendBookingEmail> _logger;
        private readonly IConfiguration _config;
        public SendBookingEmail(ILogger<SendBookingEmail> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }


        [Function("SendBookingEmail")]
        public async Task Run([ServiceBusTrigger("eventbookings", Connection = "ServiceBusConnection")] string message)
        {
            _logger.LogInformation("Service Bus message received!");
            BookingEmailDto? booking;

            try
            {
                booking = JsonSerializer.Deserialize<BookingEmailDto>(message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                //if (booking is null)
                //{
                //    _logger.LogWarning("Message deserialized to null. Invalid format.");
                //    return;
                //}

                //_logger.LogInformation($"Booking for {booking.UserName}, email {booking.UserEmail}, seats {booking.Seats}");
                //_logger.LogInformation("✅ Booking email sent (demo)");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize message: {Message}", message);
                return;
            }

            if (booking is null)
            {
                _logger.LogWarning("Message deserialized to null. Invalid format.");
                return;
            }
            _logger.LogInformation($"Booking for {booking.UserName}, email {booking.UserEmail}, seats {booking.Seats}");

            await SendEmailAsync(booking);
        }


        private async Task SendEmailAsync(BookingEmailDto booking)
        {
            // Get SendGrid API key from configuration
            var apiKey = _config["SendGridApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("SendGrid API key not configured.");
                return;
            }

            var client = new SendGridClient(apiKey);

            // Get sender email from configuration or fallback to a test Gmail for dev
            var fromEmail = _config["EmailFrom"] ?? "test@gmail.com";
            var from = new EmailAddress(fromEmail, "Go Event Platform");

            var to = new EmailAddress(booking.UserEmail);
            var subject = $"Your booking confirmation, {booking.UserName}";
            var plainText = $"Hi {booking.UserName}, your booking you made for the EVENT, for {booking.Seats} seats on {booking.CreatedAt:d} has been confirmed.";
            var html = $"<strong>Hi {booking.UserName}</strong>,<br/>your booking you made for the EVENT <b>{booking.Seats}</b> seats on {booking.CreatedAt:d} has been confirmed.";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);

            // ✅ Sandbox mode for dev/testing
            var useSandbox = _config.GetValue<bool>("UseSandboxMode");
            msg.MailSettings = new MailSettings
            {
                SandboxMode = new SandboxMode { Enable = useSandbox }
            };

            var response = await client.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
                _logger.LogInformation("✅ Booking email sent to {Email}", booking.UserEmail);
            else
                _logger.LogError("❌ Failed to send email to {Email}. Status: {Status}",
                                 booking.UserEmail, response.StatusCode);
        }
    }
}
    

    

