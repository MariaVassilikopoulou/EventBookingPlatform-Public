using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using GoEventFunctionApp.Models;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Azure.Communication.Email;
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

            _logger.LogInformation("Service Bus message received.");

            if (string.IsNullOrWhiteSpace(messageBody))
            {
                _logger.LogWarning("Message body is empty. Skipping.");
                return;
            }

            BookingEmailDto? booking;
            try
            {
                booking = JsonSerializer.Deserialize<BookingEmailDto>(
                    messageBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize message: {Message}", messageBody);
                return;
            }

            if (booking is null || string.IsNullOrWhiteSpace(booking.UserEmail))
            {
                _logger.LogWarning("Booking is null or missing UserEmail. Skipping.");
                return;
            }

            var connectionString = _config["AcsConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError("AcsConnectionString is missing from configuration.");
                return;
            }

            await SendEmailAsync(booking, connectionString);
        }

        private async Task SendEmailAsync(BookingEmailDto booking, string connectionString)
        {
            var fromAddress = _config["EmailFrom"] ?? "DoNotReply@da3fab03-eb81-44bd-9169-cc0b07c716fc.azurecomm.net";

            var plainText = $@"Hi {booking.UserName},

Your booking request for the event '{booking.EventName}' has been received.
Number of seats: {booking.Seats}
Booking date: {booking.CreatedAt:d}

Note: This booking is not confirmed until payment is completed.
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

            try
            {
                var client = new EmailClient(connectionString);

                var emailMessage = new EmailMessage(
                    senderAddress: fromAddress,
                    recipients: new EmailRecipients(new[] { new EmailAddress(booking.UserEmail, booking.UserName) }),
                    content: new EmailContent($"Booking request for {booking.EventName}")
                    {
                        PlainText = plainText,
                        Html = html
                    });

                var operation = await client.SendAsync(Azure.WaitUntil.Completed, emailMessage);
                _logger.LogInformation("Email sent to {Email}. OperationId: {Id}", booking.UserEmail, operation.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", booking.UserEmail);
                throw; // rethrow so the message is retried / dead-lettered
            }
        }
    }
}
