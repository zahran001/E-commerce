namespace Ecommerce.Services.EmailAPI.Messaging
{
    public interface IAzureServiceBusConsumer
    {
        // Both of them are async methods
        Task Start();
        Task Stop();
    }
}
