using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ecommerce.Shared.Extensions
{
    /// <summary>
    /// Centralized OpenTelemetry configuration for all microservices.
    ///
    /// This extension method eliminates code duplication across 6 services (5 APIs + Web MVC)
    /// by configuring tracing once in the Shared project.
    ///
    /// All services automatically get:
    /// - ASP.NET Core request/response tracing (HTTP timing)
    /// - HttpClient tracing (inter-service call timing)
    /// - Entity Framework Core query tracing (database query text and timing)
    /// - Service Bus message tracing (async flow visibility)
    /// - Jaeger exporter (visual timeline UI)
    ///
    /// Usage in Program.cs:
    /// builder.Services.AddEcommerceTracing("ServiceName");
    /// </summary>
    public static class OpenTelemetryExtensions
    {
        /// <summary>
        /// Add OpenTelemetry distributed tracing to the service.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="serviceName">Name of the service (e.g., "AuthAPI", "ProductAPI")</param>
        /// <param name="serviceVersion">Service version (default: "1.0.0")</param>
        /// <param name="configuration">Configuration object for Jaeger settings (optional)</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddEcommerceTracing(
            this IServiceCollection services,
            string serviceName,
            string serviceVersion = "1.0.0",
            IConfiguration configuration = null)
        {
            services.AddOpenTelemetry()
                .WithTracing(tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        // 1. Identify this service in traces
                        .AddSource(serviceName)

                        // 2. Set resource attributes (service metadata)
                        .SetResourceBuilder(
                            ResourceBuilder.CreateDefault()
                                .AddService(
                                    serviceName: serviceName,
                                    serviceVersion: serviceVersion)
                                .AddAttributes(new Dictionary<string, object>
                                {
                                    ["deployment.environment"] =
                                        GetEnvironment(configuration),
                                    ["host.name"] = Environment.MachineName
                                }))

                        // 3. Auto-instrument ASP.NET Core (HTTP requests)
                        // Traces incoming HTTP requests, response times, status codes
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;

                            // Filter out noise (health checks, swagger)
                            options.Filter = (httpContext) =>
                                !httpContext.Request.Path.StartsWithSegments("/health")
                                && !httpContext.Request.Path.StartsWithSegments("/swagger")
                                && !httpContext.Request.Path.StartsWithSegments("/healthz");
                        })

                        // 4. Auto-instrument HttpClient (inter-service calls)
                        // Traces calls from ShoppingCartAPI → ProductAPI, etc.
                        .AddHttpClientInstrumentation(options =>
                        {
                            options.RecordException = true;
                        })

                        // 5. Auto-instrument Entity Framework Core (database queries)
                        // Traces EF Core queries, execution time, SQL text
                        .AddEntityFrameworkCoreInstrumentation()

                        // 6. CRITICAL: Auto-instrument Service Bus
                        // Enables tracing of message publish/consume (Phase 3 gap!)
                        // Without this, async flows appear disconnected in Jaeger
                        .AddSource("Azure.Messaging.ServiceBus")

                        // 7. Export to Jaeger (visual timeline UI)
                        // Configurable via appsettings.json
                        .AddJaegerExporter(options =>
                        {
                            // Read from configuration if provided, else use defaults
                            var jaegerSection = configuration?.GetSection("Jaeger");

                            options.AgentHost = jaegerSection?.GetValue<string>("AgentHost")
                                ?? "localhost";
                            options.AgentPort = jaegerSection?.GetValue<int>("AgentPort")
                                ?? 6831;
                        });
                });

            return services;
        }

        /// <summary>
        /// Helper: Get environment name from configuration or runtime.
        /// </summary>
        private static string GetEnvironment(IConfiguration configuration)
        {
            // Try to get from ASPNETCORE_ENVIRONMENT
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.IsNullOrEmpty(env))
                return env;

            // Fallback to Development
            return "Development";
        }
    }
}
