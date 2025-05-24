using Ecommerce.Services.EmailAPI.Messaging;
using System.Runtime.CompilerServices;
using static System.Net.Mime.MediaTypeNames;

namespace Ecommerce.Services.EmailAPI.Extension
{
    public static class ApplicationBuilderExtensions
    {
        // Add an implementation of IAzureServiceBusConsumer
        private static IAzureServiceBusConsumer ServiceBusConsumer { get; set; } 

        public static IApplicationBuilder UseAzureServiceBusConsumer(this IApplicationBuilder app) // extension method for IApplicationBuilder
        {
            ServiceBusConsumer = app.ApplicationServices.GetService<IAzureServiceBusConsumer>();
            var hostApplicationLifetime = app.ApplicationServices.GetService<IHostApplicationLifetime>();

            // Lifecycle Hooks
            hostApplicationLifetime.ApplicationStarted.Register(OnStart);
            hostApplicationLifetime.ApplicationStopping.Register(OnStop);

            return app;
        }

        private static void OnStop()
        {
            ServiceBusConsumer.Stop();
        }

        private static void OnStart()
        {
            ServiceBusConsumer.Start();
        }
    }
}
