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
            
            var apiKey = _config["SendGridApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("SendGrid API key not configured.");
                return;
            }

            var client = new SendGridClient(apiKey);

            
            var fromEmail = _config["EmailFrom"] ?? "test@gmail.com";
            var from = new EmailAddress(fromEmail, "Go Event Platform");

            var to = new EmailAddress(booking.UserEmail);
            var subject = $" Booking request for {booking.EventName}";

            var plainText =
            $@"Hi {booking.UserName},

                Your booking request for the event '{booking.EventName}'has been received.
                Number of seats: {booking.Seats}
                Booking date: {booking.CreatedAt:d}

                ⚠️ Note: This booking is not confirmed until payment is completed. 
                Please follow the instructions to complete your registration.

                Thank you for using Flowvent!";

                            var html =
                            $@"<p><strong>Hi {booking.UserName},</strong></p>
                <p>Your booking request for the event '<strong>{booking.EventName}</strong>' been received.</p>
                <ul>
                <li>Number of seats: <strong>{booking.Seats}</strong></li>
                <li>Booking date: {booking.CreatedAt:d}</li>
                </ul>
                <p>⚠️ <strong>Note:</strong> This booking is not confirmed until payment is completed. 
                Please follow the instructions to complete your registration.</p>
                <p>Thank you for using <strong>Flowvent</strong>!</p>";
                            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);

            var useSandbox = _config.GetValue<bool>("UseSandboxMode");
            msg.MailSettings = new MailSettings
            {
                SandboxMode = new SandboxMode { Enable = useSandbox }
            };

            var response = await client.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Booking email sent to {Email}", booking.UserEmail);
            else
                _logger.LogError("Failed to send email to {Email}. Status: {Status}",
                                 booking.UserEmail, response.StatusCode);
        }
    }
}
    

    

