# Observability Implementation Guide - Step by Step

> **Goal**: Add Serilog structured logging, Correlation IDs, and OpenTelemetry distributed tracing to your E-commerce microservices
>
> **Estimated Time**: 8-12 hours
> **Cost**: $0 (all open-source tools)
> **Prerequisites**: Visual Studio 2022, all 5 microservices running locally

## ⚠️ Current Limitations

**External API Support**: This implementation currently supports correlation ID tracking only for **internal communication** between your microservices. The following are **not yet supported**:

- Correlation ID propagation to third-party APIs
- Receiving correlation IDs from external services
- Cross-tenant/cross-environment tracing

**Workaround**: For calls to external APIs, the correlation ID is available in your logs (`CorrelationId` field) but won't appear in the external service's logs unless they also implement this pattern.

---

## 📋 What You'll Accomplish

After completing this guide, you'll be able to:

✅ **Debug production issues in <5 minutes** by searching one correlation ID
✅ **See visual traces** of requests flowing across all services in Jaeger
✅ **Query structured logs** in Seq with powerful filters (service, level, correlation ID)
✅ **Replace all Console.WriteLine** with proper ILogger calls
✅ **Track Service Bus messages** from publisher to consumer with the same correlation ID

---

## 🎯 Implementation Phases

| Phase | What You'll Do | Time | Tools Needed |
|-------|----------------|------|--------------|
| **[Phase 0](#phase-0-setup-development-tools)** | Install Seq + Jaeger with Docker | 10 min | Docker Desktop |
| **[Phase 1](#phase-1-add-serilog-to-authapi)** | Add Serilog to AuthAPI (test one service first) | 1 hour | Visual Studio 2022 |
| **[Phase 2](#phase-2-roll-out-serilog-to-all-services)** | Copy Serilog config to other 4 services | 30 min | Copy/paste |
| **[Phase 3](#phase-3-add-correlation-id-middleware)** | Track requests across all services | 1.5 hours | Visual Studio 2022 |
| **[Phase 4](#phase-4-add-opentelemetry-tracing)** | Visual distributed tracing with Jaeger | 2 hours | Visual Studio 2022 |
| **[Phase 5](#phase-5-verify-everything-works)** | End-to-end testing | 30 min | Web browser |

---

## Phase 0: Setup Development Tools

### Install Seq (Log Viewer)

**1. Open PowerShell (Admin)**

**2. Run Seq in Docker:**
```powershell
docker run -d --name seq -e ACCEPT_EULA=Y -p 5341:80 datalust/seq:latest
```

**3. Verify Seq is running:**
- Open browser: http://localhost:5341
- You should see the Seq dashboard (empty for now)

**4. If you see errors:**
```powershell
# Stop and remove old container
docker stop seq
docker rm seq

# Re-run the docker run command from step 2
```

---

### Install Jaeger (Distributed Tracing Viewer)

**1. In PowerShell:**
```powershell
docker run -d --name jaeger `
  -e COLLECTOR_ZIPKIN_HOST_PORT=:9411 `
  -p 5775:5775/udp `
  -p 6831:6831/udp `
  -p 6832:6832/udp `
  -p 5778:5778 `
  -p 16686:16686 `
  -p 14268:14268 `
  -p 14250:14250 `
  -p 9411:9411 `
  jaegertracing/all-in-one:latest
```

**2. Verify Jaeger is running:**
- Open browser: http://localhost:16686
- You should see the Jaeger UI with "Search" dropdown (empty for now)

**3. Check both containers are running:**
```powershell
docker ps
```

You should see both `seq` and `jaeger` in the list.

✅ **Checkpoint**: Both Seq and Jaeger dashboards load in your browser

---

## Phase 1: Add Serilog to AuthAPI

> **Why start with AuthAPI?** It's the simplest service to test - just register a user and see logs appear!

### Step 1.1: Add NuGet Packages to AuthAPI

**1. In Visual Studio, open Solution Explorer**

**2. Right-click `E-commerce.Services.AuthAPI` → Manage NuGet Packages**

**3. Click "Browse" tab, search and install these packages (one at a time):**
   - `Serilog.AspNetCore` (version 8.0.0 or latest)
   - `Serilog.Sinks.Console` (version 5.0.1 or latest)
   - `Serilog.Sinks.File` (version 5.0.0 or latest)
   - `Serilog.Sinks.Seq` (version 7.0.1 or latest)
   - `Serilog.Enrichers.Environment` (version 2.3.0 or latest)
   - `Serilog.Enrichers.Thread` (version 3.2.0 or latest)

**4. Close NuGet Package Manager**

✅ **Checkpoint**: AuthAPI.csproj should now have 6 new Serilog packages

---

### Step 1.2: Configure Serilog in appsettings.json

**1. Open `E-commerce.Services.AuthAPI/appsettings.json`**

**2. **DELETE** the existing `"Logging"` section (lines starting with `"Logging": {`)**

**3. Add this **Serilog** section instead** (paste at the top, after the opening `{`):

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
          "path": "logs/authapi-.log",
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
  },

  "ConnectionStrings": {
    ...
```

**4. Save the file (Ctrl+S)**

---

### Step 1.3: Configure Serilog in Program.cs

**1. Open `E-commerce.Services.AuthAPI/Program.cs`**

**2. At the very top, add these using statements:**

```csharp
using Serilog;
```

**3. Find this line:**
```csharp
var builder = WebApplication.CreateBuilder(args);
```

**4. **Immediately after** that line, add:**

```csharp
// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Service", "AuthAPI")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .CreateLogger();

builder.Host.UseSerilog();
```

**5. Find this line near the bottom:**
```csharp
var app = builder.Build();
```

**6. **REPLACE** everything from `var app = builder.Build();` to `app.Run();` with:**

```csharp
var app = builder.Build();

try
{
    Log.Information("Starting AuthAPI service");

    // ... keep all existing middleware (app.UseSwagger, app.UseHttpsRedirection, etc.) ...

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AuthAPI terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
```

**7. Save the file (Ctrl+S)**

✅ **Checkpoint**: No red squiggly lines in Program.cs

---

### Step 1.4: Add ILogger to AuthAPIController

**1. Open `E-commerce.Services.AuthAPI/Controllers/AuthAPIController.cs`**

**2. Find the class constructor** (should look like):
```csharp
public AuthAPIController(IAuthService authService, ...)
```

**3. **Add ILogger as a parameter and field**:**

**Before:**
```csharp
private readonly IAuthService _authService;
private readonly ResponseDto _response;

public AuthAPIController(IAuthService authService)
{
    _authService = authService;
    _response = new ResponseDto();
}
```

**After:**
```csharp
private readonly IAuthService _authService;
private readonly ILogger<AuthAPIController> _logger;
private readonly ResponseDto _response;

public AuthAPIController(IAuthService authService, ILogger<AuthAPIController> logger)
{
    _authService = authService;
    _logger = logger;
    _response = new ResponseDto();
}
```

**4. Find the `Register` method** (line ~30-50)

**5. Add logging statements:**

**Before:**
```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegistrationRequestDto model)
{
    var errorMessage = await _authService.Register(model);
    if (!string.IsNullOrEmpty(errorMessage))
    {
        _response.IsSuccess = false;
        _response.Message = errorMessage;
        return BadRequest(_response);
    }

    return Ok(_response);
}
```

**After:**
```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegistrationRequestDto model)
{
    _logger.LogInformation("Registration attempt for user {Email}", model.Email);

    var errorMessage = await _authService.Register(model);
    if (!string.IsNullOrEmpty(errorMessage))
    {
        _logger.LogWarning("Registration failed for {Email}: {Error}", model.Email, errorMessage);
        _response.IsSuccess = false;
        _response.Message = errorMessage;
        return BadRequest(_response);
    }

    _logger.LogInformation("User {Email} registered successfully", model.Email);
    return Ok(_response);
}
```

**6. Save the file (Ctrl+S)**

---

### Step 1.5: Test Serilog with AuthAPI

**1. In Visual Studio, set AuthAPI as startup project:**
   - Right-click `E-commerce.Services.AuthAPI` → Set as Startup Project

**2. Press F5 to run**

**3. Watch the Console output - you should see colored logs:**
```
[10:23:45 INF] Starting AuthAPI service
[10:23:46 INF] Microsoft.Hosting.Lifetime | Now listening on: https://localhost:7002
```

**4. Open Seq in browser: http://localhost:5341**
   - You should see logs appearing!
   - Click on any log entry to see full details

**5. Test registration:**
   - Open Swagger: https://localhost:7002/swagger
   - Expand `POST /api/auth/register`
   - Click "Try it out"
   - Enter:
     ```json
     {
       "email": "test@example.com",
       "name": "Test User",
       "phoneNumber": "1234567890",
       "password": "Test123!"
     }
     ```
   - Click "Execute"

**6. Check Seq again - you should see:**
```
[INF] Registration attempt for user test@example.com
[INF] User test@example.com registered successfully
```

**7. If you see errors, check:**
   - Is Seq container running? `docker ps`
   - Are there red errors in Visual Studio output window?
   - Is appsettings.json valid JSON? (Use Edit → Advanced → Format Document)

✅ **Checkpoint**: Logs appear in both Console and Seq dashboard

**8. Stop AuthAPI (Shift+F5)**

---

## Phase 2: Roll Out Serilog to All Services

> Now that Serilog works in AuthAPI, copy the same config to the other 4 services

### Step 2.1: Add Serilog to ProductAPI

**1. Add NuGet packages to ProductAPI** (same 6 packages as AuthAPI):
   - Right-click `E-commerce.Services.ProductAPI` → Manage NuGet Packages
   - Install the same 6 Serilog packages from Step 1.1

**2. Copy appsettings.json Serilog config:**
   - Open `E-commerce.Services.ProductAPI/appsettings.json`
   - Delete the `"Logging"` section
   - Paste the `"Serilog"` section from AuthAPI's appsettings.json
   - **Change the file path** from `"authapi-.log"` to `"productapi-.log"`
   - **Change the Service property** from `"AuthAPI"` to `"ProductAPI"`
   - Save (Ctrl+S)

**3. Copy Program.cs Serilog setup:**
   - Open `E-commerce.Services.ProductAPI/Program.cs`
   - Add `using Serilog;` at the top
   - Copy the Serilog configuration from AuthAPI's Program.cs (after `var builder = ...`)
   - **Change `"AuthAPI"` to `"ProductAPI"`** in both places:
     ```csharp
     .Enrich.WithProperty("Service", "ProductAPI")
     ...
     Log.Information("Starting ProductAPI service");
     ...
     Log.Fatal(ex, "ProductAPI terminated unexpectedly");
     ```
   - Save (Ctrl+S)

**4. Add ILogger to ProductAPIController:**
   - Open `E-commerce.Services.ProductAPI/Controllers/ProductAPIController.cs`
   - Add `private readonly ILogger<ProductAPIController> _logger;` field
   - Add `ILogger<ProductAPIController> logger` to constructor parameter
   - Assign `_logger = logger;` in constructor
   - Find the `Get()` method and add logging:

**Before:**
```csharp
[HttpGet]
public ResponseDto Get()
{
    try
    {
        IEnumerable<Product> objList = _db.Products.ToList();
        _response.Result = _mapper.Map<IEnumerable<ProductDto>>(objList);
    }
    catch (Exception ex)
    {
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

**After:**
```csharp
[HttpGet]
public ResponseDto Get()
{
    try
    {
        _logger.LogInformation("Fetching all products");
        IEnumerable<Product> objList = _db.Products.ToList();
        _response.Result = _mapper.Map<IEnumerable<ProductDto>>(objList);
        _logger.LogInformation("Successfully retrieved {Count} products", objList.Count());
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching products");
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

   - Save (Ctrl+S)

---

### Step 2.2: Add Serilog to CouponAPI

**Repeat Step 2.1 for CouponAPI:**
1. Add 6 NuGet packages
2. Update appsettings.json (change to `"couponapi-.log"` and `"CouponAPI"`)
3. Update Program.cs (change to `"CouponAPI"`)
4. Add ILogger to CouponAPIController

---

### Step 2.3: Add Serilog to ShoppingCartAPI

**Repeat Step 2.1 for ShoppingCartAPI:**
1. Add 6 NuGet packages
2. Update appsettings.json (change to `"shoppingcartapi-.log"` and `"ShoppingCartAPI"`)
3. Update Program.cs (change to `"ShoppingCartAPI"`)
4. **IMPORTANT**: Fix the Console.WriteLine anti-pattern:

**Open: `E-commerce.Services.ShoppingCartAPI/Controllers/CartAPIController.cs`**

**Find line ~202 and ~241:**
```csharp
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex}"); // ❌ Remove this
}
```

**Replace with:**
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error in cart operation");
    _response.Message = ex.InnerException?.Message ?? ex.Message;
    _response.IsSuccess = false;
}
```

---

### Step 2.4: Add Serilog to EmailAPI

**Repeat Step 2.1 for EmailAPI:**
1. Add 6 NuGet packages
2. Update appsettings.json (change to `"emailapi-.log"` and `"EmailAPI"`)
3. Update Program.cs (change to `"EmailAPI"`)
4. **IMPORTANT**: Fix Console.WriteLine in AzureServiceBusConsumer:

**Open: `Ecommerce.Services.EmailAPI/Messaging/AzureServiceBusConsumer.cs`**

**Add at top of file:**
```csharp
using Microsoft.Extensions.Logging;
```

**Add ILogger field and constructor parameter:**
```csharp
private readonly ILogger<AzureServiceBusConsumer> _logger;

public AzureServiceBusConsumer(..., ILogger<AzureServiceBusConsumer> logger)
{
    // ... existing parameters ...
    _logger = logger;

    // ... rest of constructor ...
}
```

**Find lines 78, 97, 104 with `Console.WriteLine(ex.ToString());`**

**Replace with:**
```csharp
_logger.LogError(ex, "Error processing Service Bus message");
```

**Update constructor registration in Program.cs:**

Find where `AzureServiceBusConsumer` is registered and ensure ILogger is injected:
```csharp
// Should be registered as singleton with all dependencies
```

---

### Step 2.5: Test All Services

**1. Configure Multiple Startup Projects:**
   - Right-click Solution in Solution Explorer → Set Startup Projects
   - Select "Multiple startup projects"
   - Set these to "Start":
     - E-commerce.Services.AuthAPI
     - E-commerce.Services.ProductAPI
     - E-commerce.Services.CouponAPI
     - E-commerce.Services.ShoppingCartAPI
     - Ecommerce.Services.EmailAPI
     - E-commerce.Web
   - Click OK

**2. Press F5 to start all services**

**3. Watch the Console - you should see logs from ALL services:**
```
[10:30:01 INF] Starting AuthAPI service
[10:30:01 INF] Starting ProductAPI service
[10:30:01 INF] Starting CouponAPI service
[10:30:01 INF] Starting ShoppingCartAPI service
[10:30:01 INF] Starting EmailAPI service
```

**4. Open Seq: http://localhost:5341**

**5. Click the filter dropdown, select "Service"**
   - You should see: AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI

**6. Test each service:**
   - Open https://localhost:7000/swagger (ProductAPI)
   - Test GET /api/product
   - Check Seq - should see "Fetching all products" from ProductAPI

✅ **Checkpoint**: Seq shows logs from all 5 services with different colors/services

---

## Phase 3: Add Correlation ID Middleware

> **Purpose**: Enable request tracing across all microservices with unified correlation IDs
> **Status**: See [PHASE3-CORRELATION-ID-IMPLEMENTATION.md](PHASE3-CORRELATION-ID-IMPLEMENTATION.md) for complete, detailed guide
> **Estimated Time**: 2.5 hours
> **Complexity**: Medium

### Overview

After Phase 3, you'll be able to:
- Track any request across all 6 services using a single correlation ID
- Search one correlation ID in Seq and see the complete request journey
- Debug production issues in minutes instead of hours
- Identify which service is causing delays in a multi-service flow

### Architecture

```
┌─────────────┐
│   Web MVC   │ generates new correlation ID
└──────┬──────┘
       │ X-Correlation-ID: abc-123
       ↓
┌──────────────────────┐
│ ShoppingCartAPI      │ receives ID via middleware
└──────┬───────────────┘
       │ (calls downstream with same ID)
       ├─────────────────────┐
       │                     │
       ↓                     ↓
   ProductAPI           CouponAPI
   (receives ID)        (receives ID)
       │                     │
       └─────────────┬───────┘
                     │ publishes to Service Bus
                     │ CorrelationId: abc-123
                     ↓
            ┌─────────────────┐
            │   EmailAPI      │ reads from message metadata
            │  (consumer)     │ same correlation ID
            └─────────────────┘
```

### Key Components

| Component | Purpose | Location |
|-----------|---------|----------|
| **CorrelationIdMiddleware** | Extract/generate correlation ID, add to logs | E-commerce.Shared/Middleware |
| **HTTP Propagation Layer 1** | Forward ID in ShoppingCart → Product/Coupon calls | BackendAPIAuthenticationHttpClientHandler |
| **HTTP Propagation Layer 2** | Forward ID in Web → API calls | BaseService.cs |
| **Service Bus Propagation** | Embed ID in Service Bus messages | MessageBus.cs |
| **Consumer Integration** | Read ID from Service Bus messages | AzureServiceBusConsumer.cs |

### Implementation Steps

**Complete implementation guide**: Refer to [PHASE3-CORRELATION-ID-IMPLEMENTATION.md](PHASE3-CORRELATION-ID-IMPLEMENTATION.md)

**Quick summary of steps:**

1. **Create E-commerce.Shared project** with CorrelationIdMiddleware (30 min)
2. **Add project references** to all 6 services (10 min)
3. **Register middleware** in 5 services - skip EmailAPI (20 min)
4. **Propagate correlation ID** in HTTP calls:
   - ShoppingCartAPI → ProductAPI/CouponAPI via BackendAPIAuthenticationHttpClientHandler
   - Web → APIs via BaseService.cs
   (30 min)
5. **Propagate in Service Bus** via MessageBus.cs and register IHttpContextAccessor (20 min)
6. **Update EmailAPI consumer** to read and use correlation IDs from messages (20 min)

### Testing Strategy

**Test 1: User Registration Flow** (5 min)
- Path: Web MVC → AuthAPI → Service Bus → EmailAPI
- Verify all logs show the same CorrelationId in Seq

**Test 2: Shopping Cart Checkout** (5 min)
- Path: Web → ShoppingCartAPI → ProductAPI + CouponAPI → Service Bus → EmailAPI
- Verify correlation ID propagates across 6 services

**Test 3: Direct API Call** (5 min)
- Call ProductAPI directly via Swagger
- Verify response includes X-Correlation-ID header

### Verification Checklist

Core requirements:
- [ ] E-commerce.Shared project created with CorrelationIdMiddleware
- [ ] All 6 projects reference E-commerce.Shared
- [ ] Middleware registered in all 5 API services (NOT EmailAPI)
- [ ] BackendAPIAuthenticationHttpClientHandler propagates correlation ID
- [ ] BaseService propagates correlation ID
- [ ] MessageBus uses correlation ID from context with fallback chain
- [ ] IHttpContextAccessor registered in AuthAPI and ShoppingCartAPI
- [ ] EmailAPI consumer reads and propagates correlation IDs
- [ ] Solution builds without errors
- [ ] All 6 services start without errors

### Success Criteria

✅ One correlation ID tracks complete flow across all services
✅ Correlation ID carries through API chains and Service Bus
✅ Seq search by correlation ID returns all related logs
✅ Response headers include X-Correlation-ID
✅ All tests pass
✅ No errors on startup
✅ Web MVC tracking works

---

## Phase 4: Add OpenTelemetry Tracing (Centralized Approach)

> **Goal**: See visual timeline of requests in Jaeger UI with **zero code duplication** across 6 services
>
> **Key Innovation**: Centralize all OpenTelemetry configuration in `E-commerce.Shared` via a single extension method `AddEcommerceTracing()`
>
> **Estimated Time**: 1.5-2 hours
> **MVP Focus**: Auto-instrumentation only (no samplers, no custom Activity Sources)

### Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│        E-commerce.Shared/Extensions/OpenTelemetryExtensions.cs  │
│                                                                   │
│  public static AddEcommerceTracing(                              │
│      this IServiceCollection services,                           │
│      string serviceName,                                         │
│      IConfiguration configuration = null)                        │
│  {                                                                │
│      // Configures all 4 instrumentation types automatically:   │
│      // 1. AspNetCore (HTTP request/response)                   │
│      // 2. HttpClient (inter-service calls)                     │
│      // 3. SqlClient (database queries)                         │
│      // 4. Service Bus (async messaging)                        │
│      //                                                          │
│      // Exports to: Jaeger (localhost:6831 by default)          │
│  }                                                                │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
                            ↑ Used by all 6 services
        ┌───────────────────┼───────────────────┐
        ↓                   ↓                   ↓
    AuthAPI           ProductAPI           CouponAPI
    ShoppingCartAPI       EmailAPI         Web MVC
        ↓                   ↓                   ↓
    ┌─────────────────────────────────────────────┐
    │  In each Program.cs (ONE LINE):             │
    │  builder.Services.AddEcommerceTracing(...)  │
    └─────────────────────────────────────────────┘
                            ↓
            ┌──────────────────────────────┐
            │   Jaeger UI (localhost:16686) │
            │  Waterfall Chart with Timing   │
            │  (HTTP, SQL, Service Bus)     │
            └──────────────────────────────┘
```

---

### Step 4.1: Add OpenTelemetry Packages to E-commerce.Shared

> **Important**: Add packages to the SHARED project, not individual services. This ensures all 6 services inherit the same versions.

**1. In Solution Explorer, right-click `E-commerce.Shared` → Manage NuGet Packages**

**2. Click "Browse" tab**

**3. Install these packages (latest 1.8.x versions):**

| Package | Version | Purpose |
|---------|---------|---------|
| `OpenTelemetry` | 1.8.1+ | Core tracing library |
| `OpenTelemetry.Extensions.Hosting` | 1.8.1+ | ASP.NET Core integration |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.8.3+ | HTTP request/response tracing |
| `OpenTelemetry.Instrumentation.Http` | 1.8.1+ | HttpClient call tracing |
| `OpenTelemetry.Instrumentation.SqlClient` | 1.8.1+ | SQL Server query tracing |
| `OpenTelemetry.Exporter.Jaeger` | 1.5.0+ | Jaeger exporter |

**4. After each install, wait for "Successfully installed" message**

**5. Close NuGet Package Manager**

**6. Verify in `E-commerce.Shared.csproj`:**
   - Right-click project → Edit Project File
   - Confirm all 6 packages appear in `<ItemGroup>`
   - Close the file

✅ **Checkpoint**: E-commerce.Shared.csproj has all 6 OpenTelemetry packages

---

### Step 4.2: Create OpenTelemetryExtensions.cs in E-commerce.Shared

> This is the heart of Phase 4: **one file, used by 6 services, zero duplication**

**1. In Solution Explorer, right-click `E-commerce.Shared/Extensions` → Add → Class**

**2. Name it: `OpenTelemetryExtensions.cs`**

**3. Replace the entire file with this code:**

```csharp
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
    /// - SQL Server query tracing (database query text and timing)
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

                        // 5. Auto-instrument SQL Server (database queries)
                        // Traces EF Core queries, execution time, SQL text
                        .AddSqlClientInstrumentation(options =>
                        {
                            // Include SQL query text in traces (helps debugging in dev)
                            // In production, set to false to avoid logging PII
                            options.SetDbStatementForText = true;
                            options.RecordException = true;
                        })

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
```

**4. Save the file (Ctrl+S)**

**5. Verify no errors:**
   - Should see no red squiggly lines
   - If you see "The type 'OpenTelemetry' could not be found", rebuild the solution (Ctrl+Shift+B)

✅ **Checkpoint**: OpenTelemetryExtensions.cs created in E-commerce.Shared/Extensions

---

### Step 4.3: Update appsettings.json in Each Service

> Add Jaeger configuration section so services can override the default host/port if needed

**For EACH service** (AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI, Web):

**1. Open the service's `appsettings.json`**

**2. Add this section** (right after the opening `{` or before ConnectionStrings):

```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
```

**Example for AuthAPI:**
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  ...
}
```

**3. Save each file (Ctrl+S)**

**Optional**: In production, change `localhost` to your Jaeger hostname (e.g., `jaeger.prod.internal`), but for MVP leave as `localhost`.

✅ **Checkpoint**: All 6 services have Jaeger config in appsettings.json

---

### Step 4.4: Update Program.cs in All 6 Services (One Line Each!)

> This is where the magic happens: **one line of code per service**, all configuration in the Shared library

**For EACH service** (AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI, Web):

**1. Open the service's `Program.cs`**

**2. Add using statement at the top:**
```csharp
using Ecommerce.Shared.Extensions;
```

**3. Find the section where services are registered** (looks like):
```csharp
// Add services to the container
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton(mapper);
// ... other registrations ...
```

**4. Add this ONE LINE** (before `var app = builder.Build();`):
```csharp
builder.Services.AddEcommerceTracing("ServiceName", configuration: builder.Configuration);
```

**Replace `ServiceName` with:**
- AuthAPI → `"AuthAPI"`
- ProductAPI → `"ProductAPI"`
- CouponAPI → `"CouponAPI"`
- ShoppingCartAPI → `"ShoppingCartAPI"`
- EmailAPI → `"EmailAPI"`
- Web MVC → `"Web"`

**Full Example (AuthAPI):**
```csharp
// ... existing code ...

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton(mapper);
builder.AddAppAuthentication();  // JWT config

// ✨ ADD THIS LINE:
builder.Services.AddEcommerceTracing("AuthAPI", configuration: builder.Configuration);

var app = builder.Build();

// ... rest of Program.cs ...
```

**5. Save each Program.cs file**

**6. Rebuild solution (Ctrl+Shift+B)**
   - Should have no errors
   - If you see "The type 'AddEcommerceTracing' could not be found":
     - Make sure the using statement is there
     - Rebuild again

✅ **Checkpoint**: All 6 services have the one-line OpenTelemetry registration

---

### Step 4.5: Start Jaeger Container (If Not Already Running)

> You set this up in Phase 0, but verify it's still running

**1. Open PowerShell**

**2. Check if Jaeger is running:**
```powershell
docker ps
```

**3. If you see `jaeger` in the list, skip to Step 4.6**

**4. If Jaeger is NOT running, start it:**
```powershell
docker run -d --name jaeger `
  -p 6831:6831/udp `
  -p 16686:16686 `
  jaegertracing/all-in-one:latest
```

**5. Verify Jaeger is accessible:**
   - Open http://localhost:16686 in your browser
   - You should see the Jaeger UI with "Search" dropdown

✅ **Checkpoint**: Jaeger container is running and accessible

---

### Step 4.6: Test OpenTelemetry Tracing

> Now test the entire system end-to-end

**1. In Visual Studio, set all 6 services as startup projects:**
   - Right-click Solution → Set Startup Projects
   - Select "Multiple startup projects"
   - Set to "Start":
     - E-commerce.Services.AuthAPI
     - E-commerce.Services.ProductAPI
     - E-commerce.Services.CouponAPI
     - E-commerce.Services.ShoppingCartAPI
     - Ecommerce.Services.EmailAPI
     - E-commerce.Web
   - Click OK

**2. Press F5 to start all services**

**3. Wait for all services to start** (watch the Output window for startup messages)

**4. Open Seq dashboard in browser: http://localhost:5341**
   - Verify logs from all 5 services appear
   - (This proves logging from Phase 1-3 is working)

**5. Open Jaeger UI in browser: http://localhost:16686**

**6. In "Service" dropdown, select `AuthAPI`**

**7. Click "Find Traces"**
   - You should see traces listed!
   - Click on the first trace
   - You should see a **waterfall timeline** showing:
     - HTTP request to `/api/auth/...`
     - SQL query spans (colored differently)
     - Total request duration

**Example of what you'll see:**
```
POST /api/auth/login
├─ Span: AuthAPI (5ms total)
│  ├─ Call to database (2ms)
│  └─ HTTP response (3ms)
```

**8. Test inter-service call (the payoff!):**
   - Open Web MVC: https://localhost:7230
   - Register a user
   - Log in
   - Browse products (this calls ProductAPI)
   - Add product to cart (this calls ShoppingCartAPI → ProductAPI + CouponAPI)

**9. In Jaeger, select `ShoppingCartAPI` from Service dropdown**
   - Find the recent "CartUpsert" or similar trace
   - **Click on it**
   - **You should see a WATERFALL showing all steps:**
     ```
     POST /api/cart/CartUpsert
     ├─ ShoppingCartAPI (100ms total)
     │  ├─ Call to ProductAPI (20ms)
     │  ├─ Call to CouponAPI (10ms)
     │  ├─ SQL query (15ms)
     │  └─ Response (55ms)
     ```

**10. Explore the trace details:**
   - Hover over spans to see exact timings
   - Click on span tags to see metadata (correlation_id, product_id, etc.)
   - Red spans = SQL queries
   - Green spans = HTTP calls
   - Blue spans = Other operations

✅ **Checkpoint**: Jaeger shows waterfall traces with timing for all services

---

### Step 4.7: Verify Correlation ID Integration

> Confirm that Phase 3 correlation IDs link to Phase 4 OpenTelemetry spans

**1. In Jaeger, click on any trace span**

**2. Look for tags section** (usually on the right)

**3. You should see tags like:**
   - `correlation_id: 96ebdbee-45fa-4264-a1b8-c1be5759f40d`
   - `http.method: POST`
   - `http.target: /api/cart/CartUpsert`
   - `http.status_code: 200`

**4. Copy the `correlation_id` value**

**5. Open Seq (http://localhost:5341)**

**6. Search Seq by that correlation ID:**
   - In the search box, type `CorrelationId = "96ebdbee-45fa-4264-a1b8-c1be5759f40d"`
   - Press Enter
   - You should see **the same correlation ID** in Seq logs

**Result**: Phase 3 (Seq logs) and Phase 4 (Jaeger traces) are now linked!

✅ **Checkpoint**: Correlation IDs appear in both Seq and Jaeger spans

---

### Step 4.8: Test Service Bus Message Tracing

> This is the critical piece that Phase 3 was missing!

**1. In Web MVC, register a new user:**
   - Email: `servicebus-test@example.com`
   - Password: `Test123!`

**2. In Jaeger:**
   - Select `AuthAPI` from Service dropdown
   - Find the recent `/api/auth/register` trace
   - Click on it
   - You should see spans for:
     ```
     POST /api/auth/register
     ├─ SQL INSERT (user creation)
     ├─ Service Bus publish (NEW! Phase 4 addition)
     └─ Response
     ```

**3. Wait 5 seconds** (for EmailAPI to consume the message)

**4. In Jaeger:**
   - Select `EmailAPI` from Service dropdown
   - Find a recent trace
   - Click on it
   - You should see a span for consuming the Service Bus message
   - **The Service Bus span should show latency between publish and consume!**

**5. Compare timings in Seq:**
   - Open Seq
   - Search for `servicebus-test@example.com`
   - You should see:
     ```
     [AuthAPI] Registration attempt
     [AuthAPI] Publishing to loguser queue
     [EmailAPI] Received message from loguser queue  ← timing shows queue delay
     [EmailAPI] Email processed
     ```

✅ **Checkpoint**: Service Bus message flow is visible in Jaeger with timing

---

### What You're Now Seeing in Jaeger

| Component | Before Phase 4 | After Phase 4 |
|-----------|---|---|
| HTTP Requests | ❌ Not visible | ✅ Full timeline with duration |
| Database Queries | ❌ Not visible | ✅ Query text + execution time |
| Inter-Service Calls | ❌ Not visible | ✅ Call timing + response codes |
| Service Bus Messages | ❌ Not visible | ✅ Publish/consume timing + queue latency |
| Correlation IDs | ✅ In Seq logs | ✅ Also in Jaeger span tags |

---

### Troubleshooting

#### Problem: No traces in Jaeger

**Checklist:**
1. Is Jaeger running? `docker ps` should show `jaeger`
2. Is http://localhost:16686 accessible?
3. Did you add the using statement to Program.cs?
4. Did you rebuild the solution?
5. Did you pass `configuration: builder.Configuration` to `AddEcommerceTracing`?

**Fix:**
```powershell
# Restart Jaeger
docker restart jaeger

# Rebuild solution in Visual Studio
Ctrl+Shift+B
```

---

#### Problem: Traces show but without SQL query text

**Cause**: SQL instrumentation might not be enabled

**Check**: In Seq, do you see SQL query logs from Phase 1-3? If yes, OpenTelemetry SQL should also work.

**Fix**: Ensure `builder.Configuration` is passed to `AddEcommerceTracing()`:
```csharp
builder.Services.AddEcommerceTracing("ServiceName", configuration: builder.Configuration);
```

---

#### Problem: Service Bus spans don't appear in Jaeger

**Cause**: This is a known limitation of OpenTelemetry + Azure SDK. The tracing might be sending but not appearing.

**Workaround**: For MVP, this is acceptable. Correlation IDs in Seq logs (Phase 3) already show the flow. Phase 4 mainly gives you HTTP and SQL visibility.

**Advanced**: If you need full Service Bus tracing, you can add custom Activity tracking in MessageBus.cs (not covered in MVP).

---

### Success Criteria

Before moving on, verify:

- [ ] All 6 services start without errors
- [ ] Jaeger UI loads at http://localhost:16686
- [ ] AuthAPI traces appear in Jaeger
- [ ] ProductAPI traces appear in Jaeger
- [ ] ShoppingCartAPI traces show calls to ProductAPI
- [ ] Waterfall timeline shows accurate timings
- [ ] Correlation IDs appear in Jaeger span tags
- [ ] Searching correlation ID in Seq shows matching logs
- [ ] SQL query text appears in Jaeger spans (Traces tab)
- [ ] No errors in Visual Studio Output window

---

## Phase 5: Verify Everything Works

### End-to-End Test Scenario

**Goal**: Register a user and track the ENTIRE flow through all systems.

**1. Start all services (F5)**

**2. Open these 3 browser tabs:**
   - Tab 1: https://localhost:7230 (Web MVC)
   - Tab 2: http://localhost:5341 (Seq logs)
   - Tab 3: http://localhost:16686 (Jaeger traces)

**3. In Web MVC, register a new user:**
   - Email: `testuser@example.com`
   - Name: `Test User`
   - Phone: `1234567890`
   - Password: `Test123!`
   - Click Register

**4. In Seq (Tab 2):**
   - Search for `testuser@example.com`
   - You should see logs from **AuthAPI** and **EmailAPI**
   - Click on the first log, copy the `CorrelationId` value
   - Clear search, paste the CorrelationId
   - You should see the COMPLETE flow:
     ```
     [AuthAPI] Registration attempt for user testuser@example.com
     [AuthAPI] User testuser@example.com registered successfully
     [AuthAPI] Publishing registration message to queue
     [EmailAPI] Received user registration message
     [EmailAPI] User registration email processed successfully
     ```

**5. In Jaeger (Tab 3):**
   - Select Service: `AuthAPI`
   - Click "Find Traces"
   - Find the recent trace for `/api/auth/register`
   - Click on it
   - You should see:
     - POST /api/auth/register
     - SQL INSERT to AspNetUsers
     - Service Bus publish (if instrumented)
     - Total time breakdown

**6. Test cross-service call:**
   - In Web MVC, add a product to cart (if logged in)
   - In Jaeger, select Service: `ShoppingCartAPI`
   - Find the recent trace
   - You should see ShoppingCartAPI calling ProductAPI and CouponAPI

**7. Check log files:**
   - Navigate to each service's folder
   - Look for `logs/` directory
   - Open the latest `.log` file
   - You should see JSON-formatted logs with CorrelationId

---

### Troubleshooting Guide

#### Problem: No logs in Seq

**Check:**
1. Is Seq container running? `docker ps` should show `seq`
2. Open http://localhost:5341 - does it load?
3. Check Visual Studio Output window for errors
4. Verify appsettings.json has correct Seq URL: `"serverUrl": "http://localhost:5341"`

**Fix:**
```powershell
docker restart seq
```

---

#### Problem: No traces in Jaeger

**Check:**
1. Is Jaeger container running? `docker ps` should show `jaeger`
2. Open http://localhost:16686 - does it load?
3. Did you add OpenTelemetry packages to all services?
4. Check Visual Studio Output for OpenTelemetry errors

**Fix:**
```powershell
docker restart jaeger
```

---

#### Problem: CorrelationId not appearing in logs

**Check:**
1. Did you add `app.UseCorrelationId();` in all Program.cs files?
2. Is it AFTER `app.UseHttpsRedirection()` but BEFORE `app.UseAuthorization()`?
3. Did you build the Shared project?
4. Did all services reference the Shared project?

**Fix:**
1. Rebuild entire solution
2. Clean solution (right-click → Clean)
3. Rebuild again
4. Restart services

---

#### Problem: Different CorrelationId in EmailAPI

**Check:**
1. Did you update `MessageBus.cs` to propagate correlation ID?
2. Did you add `IHttpContextAccessor` to MessageBus constructor?
3. Did you register `builder.Services.AddHttpContextAccessor();`?
4. Did you update EmailAPI consumer to read `message.CorrelationId`?

**Fix:**
- Review Step 3.5 and 3.6 carefully
- Set breakpoint in MessageBus.PublishMessage and verify correlationId value

---

#### Problem: Build errors after adding packages

**Error**: "Could not find package OpenTelemetry"

**Fix:**
1. Tools → NuGet Package Manager → Package Manager Settings
2. Package Sources → Click +
3. Add nuget.org if missing
4. Rebuild

**Error**: "The type or namespace name 'Serilog' could not be found"

**Fix:**
1. Right-click project → Manage NuGet Packages
2. Click "Installed" tab
3. Verify Serilog.AspNetCore is installed
4. If not, reinstall it

---

### Success Criteria Checklist

Before considering this implementation complete, verify:

- [ ] All 5 services start without errors
- [ ] Seq dashboard shows logs from all 5 services
- [ ] Logs in Seq have color-coded Service names
- [ ] Jaeger shows traces from all 5 services
- [ ] One CorrelationId connects logs across AuthAPI and EmailAPI
- [ ] Jaeger traces show timing breakdown (HTTP, SQL, total)
- [ ] Log files created in `logs/` folder for each service
- [ ] No `Console.WriteLine` statements remain in code
- [ ] ShoppingCartAPI → ProductAPI calls show in Jaeger with timing
- [ ] Searching a CorrelationId in Seq shows complete request flow

---

## 🎉 What You've Accomplished

Congratulations! You've successfully implemented:

✅ **Serilog Structured Logging**
   - All services log to Console, Files, and Seq
   - No more Console.WriteLine anti-patterns
   - Logs include service name, timestamp, level, correlation ID

✅ **Correlation IDs**
   - Track requests across all 6 services (5 APIs + Web)
   - Service Bus messages carry correlation IDs
   - Search one ID in Seq and see complete journey

✅ **OpenTelemetry Distributed Tracing**
   - Visual traces in Jaeger showing request flow
   - Timing breakdown for HTTP calls, SQL queries
   - Identify slow operations and bottlenecks

---

## 📊 What to Do Next

### Daily Development Workflow

**1. Start your dev tools:**
```powershell
docker start seq jaeger
```

**2. Start all services in Visual Studio (F5)**

**3. Keep Seq open in a browser tab:**
   - Use it to debug errors in real-time
   - Search by email, correlation ID, or service name
   - Filter by log level (Error, Warning, Info)

**4. Use Jaeger when debugging performance:**
   - Find slow API endpoints
   - See which service is causing delays
   - Identify expensive SQL queries

---

### Debugging Production Issues

**Scenario: User reports "I didn't receive my cart email"**

**1. Open Seq**

**2. Search for user's email**

**3. Find the correlation ID from their cart checkout request**

**4. Search by that correlation ID**

**5. Look for errors:**
   - Did ShoppingCartAPI publish the message?
   - Did EmailAPI receive it?
   - Was there an exception?

**6. If you see the full flow, email was sent. If EmailAPI log is missing, Service Bus issue.**

---

### Performance Optimization

**1. Open Jaeger**

**2. Select Service: `ShoppingCartAPI`**

**3. Sort traces by duration (slowest first)**

**4. Click on slowest trace:**
   - If SQL query is slow: Add database index
   - If HTTP call is slow: Check downstream service
   - If total time is slow: Enable caching

---

### Advanced: Adding Logging to New Endpoints

**When you create a new API endpoint:**

```csharp
[HttpPost("my-new-endpoint")]
public async Task<IActionResult> MyNewEndpoint([FromBody] MyDto dto)
{
    _logger.LogInformation("Processing my-new-endpoint for {SomeId}", dto.Id);

    try
    {
        // ... business logic ...

        _logger.LogInformation("Successfully processed my-new-endpoint for {SomeId}", dto.Id);
        return Ok(response);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing my-new-endpoint for {SomeId}", dto.Id);
        return StatusCode(500, new { error = ex.Message });
    }
}
```

**Logging best practices:**
- Use structured placeholders `{PropertyName}` instead of string interpolation
- Log at entry and exit of important operations
- Always log exceptions with `_logger.LogError(ex, ...)`
- Include relevant IDs (userId, productId, etc.) in log messages

---

### Adding to README

Consider adding this section to your main README.md:

```markdown
## 🔍 Observability

This project uses Serilog + OpenTelemetry for comprehensive observability.

### Local Development

**Start observability tools:**
```bash
docker start seq jaeger
```

**View logs:** http://localhost:5341 (Seq)
**View traces:** http://localhost:16686 (Jaeger)

### Debugging with Correlation IDs

All logs include a `CorrelationId` field that tracks requests across services:

1. Find a log entry for a user action (e.g., user registration)
2. Copy the `CorrelationId` value
3. Search Seq by that correlation ID
4. See the complete request flow across all services

See [OBSERVABILITY-IMPLEMENTATION-GUIDE.md](OBSERVABILITY-IMPLEMENTATION-GUIDE.md) for full setup instructions.
```

---

## 🆘 Need Help?

If you get stuck:

1. **Check the troubleshooting section above**
2. **Review the checklist** - did you miss a step?
3. **Check Visual Studio Output window** for error details
4. **Verify Docker containers are running**: `docker ps`
5. **Rebuild entire solution**: Right-click Solution → Rebuild Solution

---

**Estimated completion time**: 8-12 hours
**Last updated**: 2025-12-19
**Compatible with**: .NET 8.0, Visual Studio 2022
