# Observability Implementation Guide - Step by Step

> **Goal**: Add Serilog structured logging, Correlation IDs, and OpenTelemetry distributed tracing to your E-commerce microservices
>
> **Estimated Time**: 8-12 hours
> **Cost**: $0 (all open-source tools)
> **Prerequisites**: Visual Studio 2022, all 5 microservices running locally

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

> **Goal**: Track one request across all services with a single ID

### Step 3.1: Create Shared Middleware Class

**1. In Solution Explorer, right-click Solution → Add → New Project**
   - Select "Class Library"
   - Name: `E-commerce.Shared`
   - Click Create

**2. Delete the default `Class1.cs` file**

**3. Right-click `E-commerce.Shared` → Add → New Folder → Name it `Middleware`**

**4. Right-click `Middleware` folder → Add → Class**
   - Name: `CorrelationIdMiddleware.cs`
   - Click Add

**5. Replace entire file content with:**

```csharp
using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Diagnostics;

namespace Ecommerce.Shared.Middleware;

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

        // Add to response headers
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Add to Serilog logging context (automatically includes in all logs)
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Add to Activity for OpenTelemetry (Phase 4)
            Activity.Current?.SetTag("correlation_id", correlationId);

            // Store in HttpContext for later retrieval
            context.Items["CorrelationId"] = correlationId;

            await _next(context);
        }
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check if correlation ID exists in request headers (from upstream service)
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            return correlationId.ToString();
        }

        // Generate new correlation ID (first service in the chain)
        return Guid.NewGuid().ToString();
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
```

**6. Save file (Ctrl+S)**

**7. Build the Shared project:**
   - Right-click `E-commerce.Shared` → Build
   - Ensure it succeeds

---

### Step 3.2: Reference Shared Project in All Services

**For EACH of these projects:**
- E-commerce.Services.AuthAPI
- E-commerce.Services.ProductAPI
- E-commerce.Services.CouponAPI
- E-commerce.Services.ShoppingCartAPI
- Ecommerce.Services.EmailAPI
- E-commerce.Web

**Do this:**

1. Right-click the project → Add → Project Reference
2. Check `E-commerce.Shared`
3. Click OK

---

### Step 3.3: Register Middleware in All Services

**For EACH of the 5 API services** (AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI):

**1. Open Program.cs**

**2. Add using statement at top:**
```csharp
using Ecommerce.Shared.Middleware;
```

**3. Find the line `app.UseHttpsRedirection();`**

**4. **Immediately after**, add:**
```csharp
app.UseCorrelationId();
```

**Example:**
```csharp
app.UseHttpsRedirection();
app.UseCorrelationId(); // ← Add this line
app.UseAuthentication();
app.UseAuthorization();
```

**5. Save (Ctrl+S)**

---

### Step 3.4: Propagate Correlation ID in Service-to-Service Calls

**ShoppingCartAPI calls ProductAPI and CouponAPI, so we need to forward the correlation ID.**

**1. Open: `E-commerce.Services.ShoppingCartAPI/Utility/BackendAPIAuthenticationHttpClientHandler.cs`**

**2. Find the `SendAsync` method:**

**Before:**
```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
{
    var token = _contextAccessor.HttpContext.Request.Headers["Authorization"].ToString();
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));

    return await base.SendAsync(request, cancellationToken);
}
```

**After:**
```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
{
    var token = _contextAccessor.HttpContext.Request.Headers["Authorization"].ToString();
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));

    // Propagate Correlation ID to downstream services
    if (_contextAccessor.HttpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
    {
        request.Headers.Add("X-Correlation-ID", correlationId.ToString());
    }

    return await base.SendAsync(request, cancellationToken);
}
```

**3. Save (Ctrl+S)**

---

### Step 3.5: Propagate Correlation ID in Service Bus Messages

**1. Open: `Ecommerce.MessageBus/MessageBus.cs`**

**2. Add using at top:**
```csharp
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
```

**3. Add IHttpContextAccessor field and constructor:**

**Before:**
```csharp
public class MessageBus : IMessageBus
{
    private readonly string connectionString;
    public MessageBus(IConfiguration configuration)
    {
        connectionString = configuration["ServiceBusConnectionString"]
            ?? throw new ArgumentNullException(nameof(configuration));
    }
```

**After:**
```csharp
public class MessageBus : IMessageBus
{
    private readonly string connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MessageBus(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        connectionString = configuration["ServiceBusConnectionString"]
            ?? throw new ArgumentNullException(nameof(configuration));
        _httpContextAccessor = httpContextAccessor;
    }
```

**4. Update `PublishMessage` method:**

**Before:**
```csharp
ServiceBusMessage finalMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(jsonMessage))
{
    CorrelationId = Guid.NewGuid().ToString(),
};
```

**After:**
```csharp
// Get correlation ID from current HTTP context (or generate new one)
var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
                    ?? Activity.Current?.GetBaggageItem("correlation_id")
                    ?? Guid.NewGuid().ToString();

ServiceBusMessage finalMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(jsonMessage))
{
    CorrelationId = correlationId,
    ApplicationProperties =
    {
        ["CorrelationId"] = correlationId,
        ["PublishedAt"] = DateTime.UtcNow.ToString("O")
    }
};
```

**5. Save (Ctrl+S)**

**6. Register IHttpContextAccessor in services that use MessageBus:**

**Open: `E-commerce.Services.AuthAPI/Program.cs`**

**Find where services are registered, add:**
```csharp
builder.Services.AddHttpContextAccessor();
```

**Repeat for ShoppingCartAPI** (the other service that publishes messages)

---

### Step 3.6: Read Correlation ID in EmailAPI Consumer

**1. Open: `Ecommerce.Services.EmailAPI/Messaging/AzureServiceBusConsumer.cs`**

**2. Find `OnEmailShoppingCartMessageReceived` method:**

**Before:**
```csharp
private async Task OnEmailShoppingCartMessageReceived(ProcessMessageEventArgs args)
{
    var message = args.Message;
    var body = Encoding.UTF8.GetString(message.Body);

    try
    {
        CartDto objMessage = JsonConvert.DeserializeObject<CartDto>(body);
        await _emailService.EmailCartAndLog(objMessage);
        await args.CompleteMessageAsync(message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing Service Bus message");
    }
}
```

**After:**
```csharp
private async Task OnEmailShoppingCartMessageReceived(ProcessMessageEventArgs args)
{
    var message = args.Message;
    var correlationId = message.CorrelationId ?? "unknown";

    // Push correlation ID into Serilog context for all logs in this scope
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        _logger.LogInformation("Received shopping cart email message. MessageId: {MessageId}, CorrelationId: {CorrelationId}",
            message.MessageId, correlationId);

        var body = Encoding.UTF8.GetString(message.Body);

        try
        {
            CartDto objMessage = JsonConvert.DeserializeObject<CartDto>(body);
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
```

**3. Update `OnUserRegisterRequestReceived` similarly:**

```csharp
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
```

**4. Add using at top:**
```csharp
using Serilog.Context;
```

**5. Save (Ctrl+S)**

---

### Step 3.7: Test Correlation IDs

**1. Rebuild entire solution:**
   - Right-click Solution → Rebuild Solution
   - Ensure no errors

**2. Press F5 to start all services**

**3. Open Web MVC: https://localhost:7230** (or whatever port your Web runs on)

**4. Register a new user:**
   - Click Register
   - Fill in email, name, phone, password
   - Submit

**5. Open Seq: http://localhost:5341**

**6. In the search bar, type the email you registered (e.g., `test123@example.com`)**

**7. Click on one of the log entries**

**8. Look for "CorrelationId" field - copy the value (e.g., `a1b2c3d4-1234-5678-...`)**

**9. Clear search, paste the CorrelationId into search bar**

**10. You should see:**
```
[AuthAPI] Registration attempt for user test123@example.com
[AuthAPI] User test123@example.com registered successfully
[AuthAPI] Publishing registration message to queue
[EmailAPI] Received user registration message
[EmailAPI] User registration email processed successfully
```

**All with the SAME CorrelationId!**

✅ **Checkpoint**: Search one correlation ID in Seq and see the complete request journey across services

---

## Phase 4: Add OpenTelemetry Tracing

> **Goal**: See visual timeline of requests in Jaeger UI

### Step 4.1: Add OpenTelemetry NuGet Packages

**For EACH of the 5 API services** (AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI):

**1. Manage NuGet Packages → Browse tab**

**2. Install these packages:**
   - `OpenTelemetry` (latest 1.7.x)
   - `OpenTelemetry.Extensions.Hosting` (1.7.x)
   - `OpenTelemetry.Instrumentation.AspNetCore` (1.7.x)
   - `OpenTelemetry.Instrumentation.Http` (1.7.x)
   - `OpenTelemetry.Instrumentation.SqlClient` (1.7.x)
   - `OpenTelemetry.Exporter.Jaeger` (1.5.x)

**3. Close NuGet Manager**

---

### Step 4.2: Configure OpenTelemetry in AuthAPI

**1. Open: `E-commerce.Services.AuthAPI/Program.cs`**

**2. Add using statements at top:**
```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
```

**3. Find where services are registered (before `var app = builder.Build();`)**

**4. Add this configuration:**

```csharp
// OpenTelemetry Distributed Tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("AuthAPI")
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService("AuthAPI", serviceVersion: "1.0.0")
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = builder.Environment.EnvironmentName,
                        ["host.name"] = Environment.MachineName
                    }))
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = (httpContext) =>
                {
                    // Don't trace health checks or swagger
                    return !httpContext.Request.Path.StartsWithSegments("/health")
                        && !httpContext.Request.Path.StartsWithSegments("/swagger");
                };
            })
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
                options.RecordException = true;
            })
            .AddJaegerExporter(options =>
            {
                options.AgentHost = "localhost";
                options.AgentPort = 6831;
            });
    });

// Create ActivitySource for manual tracing (if needed later)
builder.Services.AddSingleton(new ActivitySource("AuthAPI"));
```

**5. Save (Ctrl+S)**

---

### Step 4.3: Copy OpenTelemetry Config to Other Services

**For ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI:**

1. Copy the entire OpenTelemetry configuration from AuthAPI's Program.cs
2. Paste into the target service's Program.cs (before `var app = builder.Build();`)
3. **Find and replace** all instances of `"AuthAPI"` with the correct service name:
   - ProductAPI → `"ProductAPI"`
   - CouponAPI → `"CouponAPI"`
   - ShoppingCartAPI → `"ShoppingCartAPI"`
   - EmailAPI → `"EmailAPI"`

---

### Step 4.4: Test OpenTelemetry with Jaeger

**1. Rebuild solution (Ctrl+Shift+B)**

**2. Press F5 to start all services**

**3. Open Web MVC and perform these actions:**
   - Register a new user
   - Log in
   - Browse products
   - Add product to cart

**4. Open Jaeger UI: http://localhost:16686**

**5. In "Service" dropdown, select `AuthAPI`**

**6. Click "Find Traces"**

**7. You should see traces appear! Click on one:**
   - You'll see a timeline showing:
     - HTTP request to AuthAPI
     - SQL query to database
     - Response time

**8. Try selecting `ShoppingCartAPI` in the Service dropdown:**
   - Find a trace and click it
   - You should see:
     - HTTP request from Web to ShoppingCartAPI
     - HTTP call from ShoppingCartAPI → ProductAPI
     - HTTP call from ShoppingCartAPI → CouponAPI
     - SQL queries
     - Total end-to-end timing

**9. Explore the visual timeline:**
   - Hover over bars to see timing
   - Click "Trace Timeline" to see detailed waterfall

✅ **Checkpoint**: Jaeger shows visual traces with timing for all services

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
