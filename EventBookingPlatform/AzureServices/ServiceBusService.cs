using Azure.Messaging.ServiceBus;
using System.Text;
using System.Text.Json;

namespace EventBookingPlatform.AzureServices
{
    public class ServiceBusService :IAsyncDisposable
    {
        private readonly ServiceBusClient _client;
        private readonly string _queueName;

        public ServiceBusService(IConfiguration config)
        {
            _client = new ServiceBusClient(config["ServiceBus:ConnectionString"]);
            _queueName = config["ServiceBus:QueueName"];
        }

        public async Task SendMessageAsync(object message)
        {
            var sender = _client.CreateSender(_queueName);
            string json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);
            await sender.SendMessageAsync(new ServiceBusMessage(body));
        }
        public async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync();
        }
    }
}
