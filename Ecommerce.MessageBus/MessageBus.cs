using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace Ecommerce.MessageBus
{
    public class MessageBus : IMessageBus
    {
        private readonly string connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MessageBus(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            connectionString = configuration["ServiceBusConnectionString"]
                ?? throw new ArgumentNullException(nameof(configuration),
                "Service Bus connection string is not configured.");
            _httpContextAccessor = httpContextAccessor;
        }

        // publish message to service bus
        public async Task PublishMessage(object message, string topic_queue_name)
        {
            await using var client = new ServiceBusClient(connectionString);

            ServiceBusSender sender = client.CreateSender(topic_queue_name);

            var jsonMessage = JsonConvert.SerializeObject(message);

            // Get correlation ID from current HTTP context (or generate new one)
            // Fallback chain: HttpContext → Activity baggage → new GUID
            string correlationId;
            var httpContextId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();

            if (httpContextId != null)
            {
                correlationId = httpContextId;
                System.Diagnostics.Debug.WriteLine($"[MessageBus] ✅ Using correlation ID from HttpContext: {correlationId}");
            }
            else
            {
                var activityId = Activity.Current?.GetBaggageItem("correlation_id");
                if (activityId != null)
                {
                    correlationId = activityId;
                    System.Diagnostics.Debug.WriteLine($"[MessageBus] ⚠️  HttpContext null, using Activity baggage: {correlationId}");
                }
                else
                {
                    correlationId = Guid.NewGuid().ToString();
                    System.Diagnostics.Debug.WriteLine($"[MessageBus] ⚠️  Both HttpContext and Activity null - FALLBACK: Generated new GUID: {correlationId}");
                }
            }

            ServiceBusMessage finalMessage = new ServiceBusMessage(Encoding
                .UTF8.GetBytes(jsonMessage))
            {
                CorrelationId = correlationId,
                ApplicationProperties =
                {
                    ["CorrelationId"] = correlationId,
                    ["PublishedAt"] = DateTime.UtcNow.ToString("O")
                }
            };

            System.Diagnostics.Debug.WriteLine($"[MessageBus] Publishing to queue '{topic_queue_name}' with CorrelationId: {correlationId}");
            await sender.SendMessageAsync(finalMessage);
            await client.DisposeAsync();
        }
    }
}
