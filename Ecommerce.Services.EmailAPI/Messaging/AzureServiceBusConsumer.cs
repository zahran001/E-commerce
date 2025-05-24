using Azure.Messaging.ServiceBus;
using E_commerce.Web.EmailAPI.Models.Dto;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Text;

namespace Ecommerce.Services.EmailAPI.Messaging
{
    public class AzureServiceBusConsumer : IAzureServiceBusConsumer
    {
        private readonly string serviceBusConnectionString;
        private readonly string emailCartQueue;
        private readonly IConfiguration _configuration;
        private ServiceBusProcessor _emailCartProcessor;

        public AzureServiceBusConsumer(IConfiguration configuration)
        {
            _configuration = configuration;
            serviceBusConnectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            emailCartQueue = _configuration.GetValue<string>("TopicAndQueueNames:EmailShoppingCartQueue");

            

            // Create a new Service Bus client on the connection string
            var client = new ServiceBusClient(serviceBusConnectionString);

            // Create a processor that we can use to process the messages - we want to listen to the queue 'EmailShoppingCartQueue'
            _emailCartProcessor = client.CreateProcessor(emailCartQueue);
        }

        public async Task Start()
        {
            _emailCartProcessor.ProcessMessageAsync += OnEmailCartRequestReceived;
            _emailCartProcessor.ProcessErrorAsync += ErrorHandler;
        }

        public async Task Stop()
        {
            await _emailCartProcessor.StopProcessingAsync();
            await _emailCartProcessor.DisposeAsync();
        }

        private async Task OnEmailCartRequestReceived(ProcessMessageEventArgs args)
        {
            // Get the message
            var message = args.Message;
            // Deserialize the message
            var body = Encoding.UTF8.GetString(message.Body);
            // We have the CartDto model in the sevice bus - deserialize it
            CartDto objMessage = JsonConvert.DeserializeObject<CartDto>(body);

            try
            {
                // TODO - try to log email
                await args.CompleteMessageAsync(message);
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine(ex.ToString());
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            // You can log the error here
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }
    }
}
