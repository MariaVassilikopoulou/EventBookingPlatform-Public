using Azure.Communication.Email;
using Azure.Messaging.ServiceBus;
using GoEventFunctionApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GoEventFunctionApp.Functions
{
    public class SendPaymentEmail
    {
        private readonly ILogger<SendPaymentEmail> _logger;
        private readonly IConfiguration _config;

        public SendPaymentEmail(ILogger<SendPaymentEmail> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        [Function("SendPaymentEmail")]
        public async Task Run([ServiceBusTrigger("paymentconfirmations", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message)
        {
            string messageBody = message.Body.ToString();

            if (string.IsNullOrWhiteSpace(messageBody))
            {
                _logger.LogWarning("Payment message body is empty. Skipping.");
                return;
            }

            PaymentEmailDto? payment;
            try
            {
                payment = JsonSerializer.Deserialize<PaymentEmailDto>(
                    messageBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize payment message: {Message}", messageBody);
                return;
            }

            if (payment is null || string.IsNullOrWhiteSpace(payment.UserEmail))
            {
                _logger.LogWarning("Payment DTO is null or missing UserEmail. Skipping.");
                return;
            }

            var connectionString = _config["AcsConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError("AcsConnectionString is missing from configuration.");
                return;
            }

            await SendEmailAsync(payment, connectionString);
        }

        private async Task SendEmailAsync(PaymentEmailDto payment, string connectionString)
        {
            var fromEmail = _config["EmailFrom"] ?? "DoNotReply@da3fab03-eb81-44bd-9169-cc0b07c716fc.azurecomm.net";
            var fromAddress = $"Flowvent <{fromEmail}>";

            var plainText = $@"Hi {payment.UserName},

Your payment for '{payment.EventName}' has been confirmed.
Seats: {payment.Seats}
Booking ID: {payment.BookingId}
Payment date: {payment.PaidAt:d}

Your seats are now secured. We look forward to seeing you there!

Thank you for using Flowvent!";

            var html = $@"<p><strong>Hi {payment.UserName},</strong></p>
<p>Your payment for '<strong>{payment.EventName}</strong>' has been confirmed.</p>
<ul>
  <li>Seats: <strong>{payment.Seats}</strong></li>
  <li>Booking ID: <code>{payment.BookingId}</code></li>
  <li>Payment date: {payment.PaidAt:d}</li>
</ul>
<p>Your seats are now secured. We look forward to seeing you there!</p>
<p>Thank you for using <strong>Flowvent</strong>!</p>";

            try
            {
                var client = new EmailClient(connectionString);

                var emailMessage = new EmailMessage(
                    senderAddress: fromAddress,
                    recipients: new EmailRecipients(new[] { new EmailAddress(payment.UserEmail, payment.UserName) }),
                    content: new EmailContent($"Payment confirmed — {payment.EventName}")
                    {
                        PlainText = plainText,
                        Html = html
                    });

                var operation = await client.SendAsync(Azure.WaitUntil.Completed, emailMessage);
                _logger.LogInformation("Payment confirmation email sent to {Email}. OperationId: {Id}", payment.UserEmail, operation.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment email to {Email}", payment.UserEmail);
                throw;
            }
        }
    }
}
