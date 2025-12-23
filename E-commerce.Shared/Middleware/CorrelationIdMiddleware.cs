using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Diagnostics;

namespace Ecommerce.Shared.Middleware;

/// <summary>
/// Middleware for generating and propagating correlation IDs across the request pipeline.
///
/// How it works:
/// 1. Checks if X-Correlation-ID header exists in incoming request
/// 2. If yes, uses that ID (came from upstream service)
/// 3. If no, generates new GUID (this is the first service)
/// 4. Adds ID to Serilog context (automatic enrichment in all logs)
/// 5. Stores in HttpContext.Items for retrieval by other services
/// 6. Adds to response headers for client tracking
/// 7. Sets Activity tag for OpenTelemetry integration (Phase 4)
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Add to response headers (for client to track)
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Add to Serilog logging context (automatically includes in all logs via LogContext enricher)
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Add to Activity for OpenTelemetry (Phase 4 will use this)
            Activity.Current?.SetTag("correlation_id", correlationId);

            // Store in HttpContext for later retrieval by services/handlers
            context.Items["CorrelationId"] = correlationId;

            // Continue with request pipeline
            await _next(context);
        }
    }

    /// <summary>
    /// Gets correlation ID from request headers if present, otherwise generates new one.
    /// </summary>
    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check if correlation ID exists in request headers (from upstream service)
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            var id = correlationId.ToString();
            System.Diagnostics.Debug.WriteLine($"[MIDDLEWARE] ✅ {context.Request.Method} {context.Request.Path} - Found X-Correlation-ID header: {id}");
            return id;
        }

        // Generate new correlation ID (this is the first service in the chain)
        var newId = Guid.NewGuid().ToString();
        System.Diagnostics.Debug.WriteLine($"[MIDDLEWARE] 🆕 {context.Request.Method} {context.Request.Path} - No header found, GENERATED NEW ID: {newId}");
        return newId;
    }
}

/// <summary>
/// Extension method for easy registration in Program.cs
/// Usage: app.UseCorrelationId();
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
