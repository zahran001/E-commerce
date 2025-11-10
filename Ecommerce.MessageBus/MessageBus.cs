using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Ecommerce.MessageBus
{
    public class MessageBus : IMessageBus
    {
        private readonly string connectionString;
        public MessageBus(IConfiguration configuration)
        {
            connectionString = configuration["ServiceBusConnectionString"]
                ?? throw new ArgumentNullException(nameof(configuration),
                "Service Bus connection string is not configured.");
        }

        // publish message to service bus
        public async Task PublishMessage(object message, string topic_queue_name)
        {
            await using var client = new ServiceBusClient(connectionString);

            ServiceBusSender sender = client.CreateSender(topic_queue_name);

            var jsonMessage = JsonConvert.SerializeObject(message);
            ServiceBusMessage finalMessage = new ServiceBusMessage(Encoding
                .UTF8.GetBytes(jsonMessage))
            {
                CorrelationId = Guid.NewGuid().ToString(),
            };
            
            await sender.SendMessageAsync(finalMessage);
            await client.DisposeAsync();
        }
    }
}
