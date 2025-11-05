using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using EventBookingPlatform.DTOs;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using SendGrid.Helpers.Mail;
using SendGrid;
using System.Text;
using Azure.Messaging.ServiceBus;

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
        public async Task Run([ServiceBusTrigger("eventbookings", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message)
        {
            string messageBody = message.Body.ToString();

            _logger.LogInformation("Service Bus message received!");
            _logger.LogInformation("Raw Service Bus message: {Message}", messageBody);

            if (string.IsNullOrWhiteSpace(messageBody))
            {
                _logger.LogWarning("Message body is empty. Skipping processing.");
                return;
            }

            // Fetch SendGrid API key from Key Vault
            var apiKey = _config["SendGridApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("❌ Cannot fetch SendGridApiKey from Key Vault.");
                return;
            }
            _logger.LogInformation("✅ Successfully fetched SendGridApiKey from Key Vault.");

            BookingEmailDto? booking;
            try
            {
                booking = JsonSerializer.Deserialize<BookingEmailDto>(
                    messageBody,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize message: {Message}", messageBody);
                return;
            }

            if (booking is null)
            {
                _logger.LogWarning("Message deserialized to null. Invalid format.");
                return;
            }

            _logger.LogInformation($"Booking for {booking.UserName}, email {booking.UserEmail}, seats {booking.Seats}");

            await SendEmailAsync(booking, apiKey);
        }

        private async Task SendEmailAsync(BookingEmailDto booking, string apiKey)
        {
            var client = new SendGridClient(apiKey);
            var fromEmail = _config["EmailFrom"] ?? "test@gmail.com";
            var from = new EmailAddress(fromEmail, "Go Event Platform");
            var to = new EmailAddress(booking.UserEmail);
            var subject = $"Booking request for {booking.EventName}";

            var plainText = $@"Hi {booking.UserName},

                Your booking request for the event '{booking.EventName}' has been received.
                Number of seats: {booking.Seats}
                Booking date: {booking.CreatedAt:d}

                ⚠️ Note: This booking is not confirmed until payment is completed.
                Please follow the instructions to complete your registration.

                Thank you for using Flowvent!";

                            var html = $@"<p><strong>Hi {booking.UserName},</strong></p>
                <p>Your booking request for the event '<strong>{booking.EventName}</strong>' has been received.</p>
                <ul>
                <li>Number of seats: <strong>{booking.Seats}</strong></li>
                <li>Booking date: {booking.CreatedAt:d}</li>
                </ul>
                <p>⚠️ <strong>Note:</strong> This booking is not confirmed until payment is completed. 
                Please follow the instructions to complete your registration.</p>
                <p>Thank you for using <strong>Flowvent</strong>!</p>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
            var useSandbox = _config.GetValue<bool>("UseSandboxMode");
            msg.MailSettings = new MailSettings { SandboxMode = new SandboxMode { Enable = useSandbox } };

            var response = await client.SendEmailAsync(msg);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Booking email sent to {Email}", booking.UserEmail);
            else
                _logger.LogError("Failed to send email to {Email}. Status: {Status}", booking.UserEmail, response.StatusCode);
        }
    }
}