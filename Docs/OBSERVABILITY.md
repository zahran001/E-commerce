# Observability Deep Dive

This project has a full observability stack: **Correlation IDs**, **Serilog + Seq** (structured logging), **Jaeger + OpenTelemetry** (distributed tracing), and **Redis** (caching).

---

## Correlation ID Implementation

Complete request tracing across all 6 microservices with unified correlation IDs.

### How It Works

- **Middleware Generation** - `CorrelationIdMiddleware` generates a unique ID for each incoming HTTP request
- **HTTP Propagation** - `BaseService` (Web -> APIs) and `BackendAPIAuthenticationHttpClientHandler` (API -> API) propagate via `X-Correlation-ID` header
- **Service Bus Integration** - `MessageBus` embeds the correlation ID in Service Bus messages for async flows
- **Unified Logging** - `Serilog.Context` enriches all log entries automatically with the correlation ID

### Example Trace Flow

```
User Action: POST /Cart/EmailCart
+-- Web MVC generates: CorrelationId: 96ebdbee-45fa-4264-a1b8-c1be5759f40d
+-- Sends to ShoppingCartAPI with header
+-- ShoppingCartAPI receives and uses same ID
+-- ShoppingCartAPI calls ProductAPI with same ID
+-- ShoppingCartAPI calls CouponAPI with same ID
+-- ShoppingCartAPI publishes to Service Bus with same ID
+-- EmailAPI consumes message with same ID

Seq Query: Search "96ebdbee-45fa-4264-a1b8-c1be5759f40d"
Result: Complete request timeline across all 6 services
```

### Key Components

- [CorrelationIdMiddleware.cs](../E-commerce.Shared/Middleware/CorrelationIdMiddleware.cs) - Generates and stores correlation IDs
- [BaseService.cs](../E-commerce.Web/Service/BaseService.cs) - Propagates to downstream APIs (Web -> APIs)
- [BackendAPIAuthenticationHttpClientHandler.cs](../E-commerce.Services.ShoppingCartAPI/Utility/BackendAPIAuthenticationHttpClientHandler.cs) - Propagates between services (API -> API)
- [MessageBus.cs](../Ecommerce.MessageBus/MessageBus.cs) - Embeds in Service Bus messages
- [AzureServiceBusConsumer.cs](../Ecommerce.Services.EmailAPI/Messaging/AzureServiceBusConsumer.cs) - Reads from messages for consumer logging

---

## OpenTelemetry / Jaeger (Distributed Tracing)

A single extension method `AddEcommerceTracing()` configures OpenTelemetry across all 6 services via [E-commerce.Shared](../E-commerce.Shared/Extensions/OpenTelemetryExtensions.cs).

### Integration

```csharp
// In Program.cs (all 6 services)
builder.Services.AddEcommerceTracing("ServiceName", configuration: builder.Configuration);
```

### Configuration

```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  }
}
```

### Auto-Instrumentation Coverage

| Component | Details |
|-----------|---------|
| **HTTP Requests** | All Web MVC and API calls traced |
| **Database Queries** | EF Core SQL Server queries with timing |
| **Inter-Service Calls** | HttpClient calls between APIs |
| **Service Bus Messages** | Publish/consume timing tracked |
| **Correlation IDs** | Linked to spans via activity tags |

### What You Can See in Jaeger

- Waterfall chart of entire request flow across all services
- Database query timing with SQL statement text (development mode)
- Service-to-Service call latencies (Web -> API -> DB cascades)
- Message queue processing delays (async flow timing)

### Local Development Setup

```bash
# Start Jaeger container
docker run -d -p 6831:6831/udp -p 16686:16686 jaegertracing/all-in-one:latest

# Open Jaeger UI
# http://localhost:16686
```

---

## Serilog (Structured Logging)

All 6 services use Serilog with Console, File, and Seq sinks.

### Configuration

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File", "Serilog.Sinks.Seq"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/[service]api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {CorrelationId} | {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

### Sinks

| Sink | Purpose | Details |
|------|---------|---------|
| **Console** | Real-time dev output | Color-coded |
| **File** | Rolling daily logs | `logs/[service]api-YYYYMMDD.log`, 7-day retention |
| **Seq** | Centralized aggregation | `http://localhost:5341`, full-text search |

### Seq Local Setup

```bash
docker run -d -e ACCEPT_EULA=Y -p 5341:80 datalust/seq
# Open Seq UI: http://localhost:5341
```

---

## Redis Caching

Product catalog and session caching via Redis, delivering ~95% latency reduction on catalog queries.

See [Redis/](Redis/) for detailed configuration and benchmarks.
