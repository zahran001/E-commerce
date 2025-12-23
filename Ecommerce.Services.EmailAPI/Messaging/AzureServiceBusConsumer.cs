using Azure.Messaging.ServiceBus;
using E_commerce.Services.EmailAPI.Models.Dto;
using Ecommerce.Services.EmailAPI.Services;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Text;
using Serilog.Context;

namespace Ecommerce.Services.EmailAPI.Messaging
{
    public class AzureServiceBusConsumer : IAzureServiceBusConsumer
    {
        private readonly string serviceBusConnectionString;
        private readonly string emailCartQueue;
        private readonly string logUserQueue;
        private readonly IConfiguration _configuration;
        private ServiceBusProcessor _emailCartProcessor;
        private ServiceBusProcessor _logUserProcessor;
        private readonly EmailService _emailService; // EmailService - registered with Singleton implementation
        private readonly ILogger<AzureServiceBusConsumer> _logger;

        public AzureServiceBusConsumer(IConfiguration configuration, EmailService emailService, ILogger<AzureServiceBusConsumer> logger)
        {
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
            serviceBusConnectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            emailCartQueue = _configuration.GetValue<string>("TopicAndQueueNames:EmailShoppingCartQueue");
			logUserQueue = _configuration.GetValue<string>("TopicAndQueueNames:LogUserQueue");


			// Create a new Service Bus client on the connection string
			var client = new ServiceBusClient(serviceBusConnectionString);

			// Create a processor that we can use to process the messages - we want to listen to the queue emailCartQueue
			_emailCartProcessor = client.CreateProcessor(emailCartQueue);

			// Create a processor that we can use to process the messages - we want to listen to the queue logUserQueue
			_logUserProcessor = client.CreateProcessor(logUserQueue);
		}

        public async Task Start()
        {
            _emailCartProcessor.ProcessMessageAsync += OnEmailCartRequestReceived;
            _emailCartProcessor.ProcessErrorAsync += ErrorHandler;
            await _emailCartProcessor.StartProcessingAsync(); // Start processing messages - signals the processor to begin processing messages from the queue

			_logUserProcessor.ProcessMessageAsync += OnUserRegisterRequestReceived;
			_logUserProcessor.ProcessErrorAsync += ErrorHandler;
			await _logUserProcessor.StartProcessingAsync();
		}

		public async Task Stop()
        {
            await _emailCartProcessor.StopProcessingAsync();
            await _emailCartProcessor.DisposeAsync();

			await _logUserProcessor.StopProcessingAsync();
			await _logUserProcessor.DisposeAsync();
		}

        private async Task OnEmailCartRequestReceived(ProcessMessageEventArgs args)
        {
            var message = args.Message;
            var correlationId = message.CorrelationId ?? "unknown";

            // Push correlation ID into Serilog context for all logs in this scope
            // This ensures all logs in the processing have the same correlation ID
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                _logger.LogInformation("Received shopping cart email message. MessageId: {MessageId}, CorrelationId: {CorrelationId}",
                    message.MessageId, correlationId);

                // Deserialize the message
                var body = Encoding.UTF8.GetString(message.Body);
                // We have the CartDto model in the sevice bus - deserialize it
                CartDto objMessage = JsonConvert.DeserializeObject<CartDto>(body);

                try
                {
                    // try to log email
                    await _emailService.EmailCartAndLog(objMessage);
                    await args.CompleteMessageAsync(message);

                    _logger.LogInformation("Shopping cart email processed successfully for CorrelationId: {CorrelationId}", correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing shopping cart email message. CorrelationId: {CorrelationId}", correlationId);
                    throw;
                }
            }
        }

		private async Task OnUserRegisterRequestReceived(ProcessMessageEventArgs args)
		{
			var message = args.Message;
			var correlationId = message.CorrelationId ?? "unknown";

			using (LogContext.PushProperty("CorrelationId", correlationId))
			{
				_logger.LogInformation("Received user registration message. MessageId: {MessageId}, CorrelationId: {CorrelationId}",
					message.MessageId, correlationId);

				var body = Encoding.UTF8.GetString(message.Body);
				string email = JsonConvert.DeserializeObject<string>(body);

				try
				{
					// try to log email
					await _emailService.LogUserEmail(email);
					await args.CompleteMessageAsync(args.Message);

					_logger.LogInformation("User registration email processed successfully for CorrelationId: {CorrelationId}", correlationId);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error processing user registration message. CorrelationId: {CorrelationId}", correlationId);
					throw;
				}
			}
		}

		private Task ErrorHandler(ProcessErrorEventArgs args)
        {
			_logger.LogError(args.Exception, "Error in Service Bus processor for source {ErrorSource}", args.ErrorSource);
			return Task.CompletedTask;
        }
    }
}
