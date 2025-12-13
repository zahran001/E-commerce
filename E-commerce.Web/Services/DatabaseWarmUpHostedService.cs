using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using E_commerce.Web.Models;

namespace E_commerce.Web.Services
{
    public class DatabaseWarmUpHostedService : IHostedService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DatabaseWarmUpHostedService> _logger;
        private readonly WarmUpConfiguration _config;
        private readonly IHostApplicationLifetime _appLifetime;

        public DatabaseWarmUpHostedService(
            IHttpClientFactory httpClientFactory,
            ILogger<DatabaseWarmUpHostedService> logger,
            IOptions<WarmUpConfiguration> config,
            IHostApplicationLifetime appLifetime)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config.Value;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_config.Enabled)
            {
                _logger.LogInformation("Database warm-up is disabled in configuration");
                return Task.CompletedTask;
            }

            _appLifetime.ApplicationStarted.Register(() =>
            {
                _ = Task.Run(async () => await ExecuteWarmUpAsync(cancellationToken), cancellationToken);
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Database warm-up service is stopping");
            return Task.CompletedTask;
        }

        private async Task ExecuteWarmUpAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting database warm-up for {Count} services", _config.Services.Count);

            try
            {
                var warmUpTasks = _config.Services
                    .Select(service => WarmUpServiceAsync(service, cancellationToken))
                    .ToArray();

                await Task.WhenAll(warmUpTasks);

                stopwatch.Stop();
                var successCount = warmUpTasks.Count(t => t.IsCompletedSuccessfully && t.Result);
                _logger.LogInformation(
                    "Database warm-up completed: {Success}/{Total} services ready in {ElapsedMs}ms",
                    successCount,
                    _config.Services.Count,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database warm-up encountered an unexpected error");
            }
        }

        private async Task<bool> WarmUpServiceAsync(ServiceWarmUpConfig service, CancellationToken cancellationToken)
        {
            var serviceName = service.Name;
            var healthUrl = $"{service.BaseUrl}{service.HealthEndpoint}";

            _logger.LogInformation("Warming up {ServiceName} at {Url}", serviceName, healthUrl);

            for (int attempt = 1; attempt <= service.MaxRetries; attempt++)
            {
                try
                {
                    using var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromMilliseconds(service.TimeoutMs);

                    var stopwatch = Stopwatch.StartNew();
                    var response = await client.GetAsync(healthUrl, cancellationToken);
                    stopwatch.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation(
                            "{ServiceName} is ready (attempt {Attempt}/{MaxRetries}, {ElapsedMs}ms)",
                            serviceName,
                            attempt,
                            service.MaxRetries,
                            stopwatch.ElapsedMilliseconds);
                        return true;
                    }

                    _logger.LogWarning(
                        "{ServiceName} returned {StatusCode} on attempt {Attempt}/{MaxRetries}",
                        serviceName,
                        response.StatusCode,
                        attempt,
                        service.MaxRetries);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning(
                        "{ServiceName} timed out after {TimeoutMs}ms on attempt {Attempt}/{MaxRetries}",
                        serviceName,
                        service.TimeoutMs,
                        attempt,
                        service.MaxRetries);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "{ServiceName} failed on attempt {Attempt}/{MaxRetries}: {Message}",
                        serviceName,
                        attempt,
                        service.MaxRetries,
                        ex.Message);
                }

                if (attempt < service.MaxRetries)
                {
                    var delayMs = service.RetryDelayMs * attempt;
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            _logger.LogError(
                "{ServiceName} failed to warm up after {MaxRetries} attempts",
                serviceName,
                service.MaxRetries);
            return false;
        }
    }
}
