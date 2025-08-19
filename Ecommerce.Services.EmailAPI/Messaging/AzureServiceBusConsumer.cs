using Azure.Messaging.ServiceBus;
using E_commerce.Services.EmailAPI.Models.Dto;
using Ecommerce.Services.EmailAPI.Services;
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
        private readonly EmailService _emailService; // EmailService - registered with Singleton implementation

        public AzureServiceBusConsumer(IConfiguration configuration, EmailService emailService)
        {
            _emailService = emailService;
            _configuration = configuration;
            serviceBusConnectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            emailCartQueue = _configuration.GetValue<string>("TopicAndQueueNames:EmailShoppingCartQueue");

            

            // Create a new Service Bus client on the connection string
            var client = new ServiceBusClient(serviceBusConnectionString);

			// Create a processor that we can use to process the messages - we want to listen to the queue emailCartQueue
			_emailCartProcessor = client.CreateProcessor(emailCartQueue);
        }

        public async Task Start()
        {
            _emailCartProcessor.ProcessMessageAsync += OnEmailCartRequestReceived;
            _emailCartProcessor.ProcessErrorAsync += ErrorHandler;

            // Start processing messages - signals the processor to begin processing messages from the queue
            await _emailCartProcessor.StartProcessingAsync();
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
                // try to log email
                await _emailService.EmailCartAndLog(objMessage);
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
