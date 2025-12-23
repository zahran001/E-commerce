# Phase 3: Correlation ID Tracking - Complete Implementation Guide

> **Phase**: 3 of 5 (Observability Implementation)
> **Purpose**: Enable request tracing across all microservices with unified correlation IDs
> **Status**: Approved for Implementation
> **Estimated Time**: 2.5 hours
> **Complexity**: Medium

## 📋 Table of Contents

1. [Overview & Prerequisites](#overview--prerequisites)
2. [Architecture & Design Review](#architecture--design-review)
3. [Design Enhancements vs Original Guide](#design-enhancements-vs-original-guide)
4. [Step-by-Step Implementation](#step-by-step-implementation)
5. [Testing Strategy](#testing-strategy)
6. [Verification Checklist](#verification-checklist)
7. [Troubleshooting](#troubleshooting)
8. [Success Criteria](#success-criteria)

---

## Overview & Prerequisites

### What Phase 3 Accomplishes

After Phase 3 completion, you'll be able to:
- Track any request across all 6 services (5 APIs + Web MVC) using a single correlation ID
- Search one correlation ID in Seq and see the complete request journey
- Debug production issues in minutes instead of hours
- Identify which service is causing delays in a multi-service flow

### Prerequisites: Phase 2 Must Be Complete

**CRITICAL**: Before starting Phase 3, verify Phase 2 completion:

#### EmailAPI Serilog Configuration ✅
- [ ] 6 Serilog NuGet packages installed
- [ ] `appsettings.json` has `"Serilog"` section
- [ ] `Program.cs` configured with `Log.Logger` and `builder.Host.UseSerilog()`
- [ ] `AzureServiceBusConsumer` has `ILogger<AzureServiceBusConsumer>` injected
- [ ] All `Console.WriteLine` replaced with `_logger` calls

#### Web MVC Serilog Configuration ✅
- [ ] 6 Serilog NuGet packages installed
- [ ] `appsettings.json` has `"Serilog"` section
- [ ] `Program.cs` configured with Serilog

#### Verification
```powershell
# Start all services (F5)
# Open Seq: http://localhost:5341
# You should see logs from: AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI, Web
```

---

## Architecture & Design Review

### Correlation Flow Pattern

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

### Why This Approach Works

1. **Serilog.Context.LogContext**: Built on AsyncLocal, flows through async/await automatically
2. **X-Correlation-ID Header**: Industry-standard header name, simple to debug with tools like Postman
3. **HttpContext.Items Storage**: Thread-safe, available to all services in request scope
4. **Service Bus CorrelationId**: Native property, preserved across retries automatically
5. **Fallback Chain**: Handles edge cases (background jobs, no HTTP context, etc.)

---

## Design Enhancements vs Original Guide

### Gap 1: Web MVC → API Propagation (CRITICAL)

**Original Guide**: Only covers API-to-API propagation via `BackendAPIAuthenticationHttpClientHandler`

**Issue**: Web MVC uses different HTTP client pattern (BaseService). Without this fix:
- Web → AuthAPI: No correlation ID
- Web → ProductAPI: No correlation ID
- Web → CartAPI: No correlation ID
- **RESULT**: Cannot track user flows from the UI

**This Guide Adds**: BaseService.cs modifications to propagate correlation ID for all Web → API calls

### Gap 2: Two HTTP Client Patterns

**Original Guide**: Assumes all services use DelegatingHandler pattern

**Reality**:
- ShoppingCartAPI uses `BackendAPIAuthenticationHttpClientHandler` (DelegatingHandler)
- Web MVC uses `BaseService` with manual `HttpRequestMessage` construction

**This Guide**: Provides separate solutions for both patterns

### Gap 3: Explicit IHttpContextAccessor Verification

**Original Guide**: Assumes services have `IHttpContextAccessor` registered

**This Guide**: Explicitly verifies and registers where needed:
- AuthAPI: Needs registration for MessageBus
- ShoppingCartAPI: Likely already registered, but verified
- Web: Already registered (line 21 of Program.cs)

---

## Step-by-Step Implementation

### Step 1: Create E-commerce.Shared Project (30 minutes)

#### 1.1: Create New Class Library

1. Open Visual Studio Solution Explorer
2. Right-click **Solution** → **Add** → **New Project**
3. Search "Class Library"
4. Select **Class Library** for C#
5. **Project name**: `E-commerce.Shared`
6. **Location**: Same as other projects (auto-filled)
7. **Target framework**: `.NET 8.0`
8. Click **Create**

#### 1.2: Clean Up Project

1. In the new `E-commerce.Shared` project, delete `Class1.cs`
2. Right-click project → **Properties** → **General**
3. Verify: **Target framework** = `.NET 8.0`

#### 1.3: Add Required NuGet Packages

Right-click project → **Manage NuGet Packages**

Search and install (in order):
1. `Microsoft.AspNetCore.Http.Abstractions` (v8.0.0 or latest)
2. `Serilog.Extensions.Hosting` (v8.0.0 or latest)
3. `System.Diagnostics.DiagnosticSource` (v8.0.0 or latest)

#### 1.4: Create Middleware Folder

1. Right-click `E-commerce.Shared` → **Add** → **New Folder**
2. Name: `Middleware`

#### 1.5: Create CorrelationIdMiddleware.cs

Right-click `Middleware` folder → **Add** → **Class**

**Name**: `CorrelationIdMiddleware.cs`

**Content**:

```csharp
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
            return correlationId.ToString();
        }

        // Generate new correlation ID (this is the first service in the chain)
        return Guid.NewGuid().ToString();
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
```

#### 1.6: Build and Verify

1. Right-click `E-commerce.Shared` → **Build**
2. Wait for build to complete
3. Verify: **Build succeeded** in output window
4. You should see **0 errors, 0 warnings**

**✅ Checkpoint**: E-commerce.Shared project created with middleware

---

### Step 2: Add Project References (10 minutes)

Add `E-commerce.Shared` reference to all 6 projects.

#### For Each Project (6 total)

**Projects**: AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI, Web

1. Right-click project → **Add** → **Project Reference**
2. Check ✓ `E-commerce.Shared`
3. Click **OK**
4. Right-click project → **Build** (verify no errors)

After completing for all 6:
```
✅ AuthAPI references E-commerce.Shared
✅ ProductAPI references E-commerce.Shared
✅ CouponAPI references E-commerce.Shared
✅ ShoppingCartAPI references E-commerce.Shared
✅ EmailAPI references E-commerce.Shared
✅ Web references E-commerce.Shared
```

**✅ Checkpoint**: All projects reference E-commerce.Shared

---

### Step 3: Register Middleware in Services (20 minutes)

Add correlation ID middleware to the request pipeline in 5 services.

**Note**: EmailAPI is **SKIPPED** because it's a Service Bus consumer with no HTTP request pipeline

#### For Each Service (5 total)

**Services**: AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, Web

**Step 3.1**: Add Using Statement

Open `Program.cs` and add at the top:

```csharp
using Ecommerce.Shared.Middleware;
```

**Step 3.2**: Register Middleware

Find this section in Program.cs:

```csharp
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
```

**Add the middleware between HTTPS and Authentication**:

```csharp
app.UseHttpsRedirection();
app.UseCorrelationId(); // ← ADD THIS LINE
app.UseAuthentication();
app.UseAuthorization();
```

**⚠️ CRITICAL**: Middleware order matters!
- Must be AFTER `UseHttpsRedirection`
- Must be BEFORE `UseAuthentication` and `UseAuthorization`
- Otherwise correlation ID won't be available to controllers

**Step 3.3**: Verify Each Service

After editing each Program.cs:
1. Right-click project → **Build**
2. Verify: **Build succeeded**

After all 5 services:
```
✅ AuthAPI: app.UseCorrelationId() registered
✅ ProductAPI: app.UseCorrelationId() registered
✅ CouponAPI: app.UseCorrelationId() registered
✅ ShoppingCartAPI: app.UseCorrelationId() registered
✅ Web: app.UseCorrelationId() registered
```

**✅ Checkpoint**: Middleware registered in all 5 services

---

### Step 4: Propagate Correlation ID in HTTP Calls (30 minutes)

Handle two different HTTP client patterns used in your services.

#### 4.1: ShoppingCartAPI → ProductAPI/CouponAPI (DelegatingHandler Pattern)

**File**: `E-commerce.Services.ShoppingCartAPI/Utility/BackendAPIAuthenticationHttpClientHandler.cs`

Find the `SendAsync` method (around lines 16-23):

**BEFORE**:
```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
{
    var token = await _accessor.HttpContext.GetTokenAsync("access_token");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await base.SendAsync(request, cancellationToken);
}
```

**AFTER** (add correlation ID propagation):
```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
{
    var token = await _accessor.HttpContext.GetTokenAsync("access_token");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // Propagate Correlation ID to downstream services (ProductAPI, CouponAPI)
    if (_accessor.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId))
    {
        request.Headers.Add("X-Correlation-ID", correlationId.ToString());
    }

    return await base.SendAsync(request, cancellationToken);
}
```

**What this does**:
1. Reads correlation ID from HttpContext.Items (set by middleware)
2. Adds it to the X-Correlation-ID header
3. When calling ProductAPI/CouponAPI, they'll receive the same correlation ID

#### 4.2: Web MVC → All APIs (Manual HttpRequestMessage Pattern)

**File**: `E-commerce.Web/Service/BaseService.cs`

##### Step 4.2.1: Add IHttpContextAccessor to Fields

Find the field declarations (around lines 17-20):

**BEFORE**:
```csharp
private readonly IHttpClientFactory _httpClientFactory;
private readonly ITokenProvider _tokenProvider;
```

**AFTER** (add new field):
```csharp
private readonly IHttpClientFactory _httpClientFactory;
private readonly ITokenProvider _tokenProvider;
private readonly IHttpContextAccessor _httpContextAccessor;
```

##### Step 4.2.2: Add to Constructor

Find the constructor (around lines 22-26):

**BEFORE**:
```csharp
public BaseService(IHttpClientFactory httpClientFactory, ITokenProvider tokenProvider)
{
    _httpClientFactory = httpClientFactory;
    _tokenProvider = tokenProvider;
}
```

**AFTER** (add parameter and assignment):
```csharp
public BaseService(IHttpClientFactory httpClientFactory, ITokenProvider tokenProvider, IHttpContextAccessor httpContextAccessor)
{
    _httpClientFactory = httpClientFactory;
    _tokenProvider = tokenProvider;
    _httpContextAccessor = httpContextAccessor;
}
```

##### Step 4.2.3: Propagate in SendAsync Method

Find the `SendAsync` method (around lines 28-50).

Locate where Authorization header is added (around line 35):

**BEFORE**:
```csharp
if (withBearer)
{
    var token = _tokenProvider.GetToken();
    message.Headers.Add("Authorization", $"Bearer {token}");
}

message.RequestUri = new Uri(requestDto.Url);
```

**AFTER** (add correlation ID propagation):
```csharp
if (withBearer)
{
    var token = _tokenProvider.GetToken();
    message.Headers.Add("Authorization", $"Bearer {token}");
}

// Propagate Correlation ID from Web MVC to downstream APIs
if (_httpContextAccessor.HttpContext?.Items.TryGetValue("CorrelationId", out var correlationId) == true)
{
    message.Headers.Add("X-Correlation-ID", correlationId.ToString());
}

message.RequestUri = new Uri(requestDto.Url);
```

**What this does**:
1. Reads correlation ID from HttpContext.Items (set by Web's middleware)
2. Adds it to X-Correlation-ID header
3. All Web → API calls carry the same correlation ID

##### Step 4.2.4: Verify IHttpContextAccessor Registration

Open `E-commerce.Web/Program.cs` and look for this line:

```csharp
builder.Services.AddHttpContextAccessor();
```

This should already exist (usually on line 21). If not present, add it with other service registrations:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("EcommerceAPI");
// ... other services
```

**✅ Checkpoint**: HTTP correlation ID propagation implemented

---

### Step 5: Propagate Correlation ID in Service Bus (20 minutes)

Enable correlation ID tracking from publishers to EmailAPI consumer.

#### 5.1: Update MessageBus.cs

**File**: `Ecommerce.MessageBus/MessageBus.cs`

##### Step 5.1.1: Add NuGet Packages

Right-click `Ecommerce.MessageBus` project → **Manage NuGet Packages**

Install:
- `Microsoft.AspNetCore.Http.Abstractions` (v2.2.0 or latest)
- `System.Diagnostics.DiagnosticSource` (v8.0.0 or latest)

##### Step 5.1.2: Add Using Statements

At the top of MessageBus.cs, add:

```csharp
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
```

##### Step 5.1.3: Update Constructor

Find the MessageBus class definition and constructor (around lines 12-18):

**BEFORE**:
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

**AFTER** (add IHttpContextAccessor injection):
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

##### Step 5.1.4: Update PublishMessage Method

Find the `PublishMessage` method and locate where `ServiceBusMessage` is created (around line 33):

**BEFORE**:
```csharp
ServiceBusMessage finalMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(jsonMessage))
{
    CorrelationId = Guid.NewGuid().ToString(),
};
```

**AFTER** (use correlation ID from context with fallback):
```csharp
// Get correlation ID from current HTTP context (or generate new one)
// Fallback chain: HttpContext → Activity baggage → new GUID
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

**What this does**:
- Uses the correlation ID from HTTP context if available
- Falls back to Activity baggage (Phase 4 compatibility)
- Falls back to new GUID if no context (background jobs)
- Stores in both `CorrelationId` property and `ApplicationProperties` for flexibility

#### 5.2: Register IHttpContextAccessor in Publisher Services

MessageBus is used by AuthAPI and ShoppingCartAPI. Ensure both have IHttpContextAccessor registered.

##### File 1: AuthAPI/Program.cs

Find the service registration section (around line 39):

**Find**: `builder.Services.AddScoped<IMessageBus, MessageBus>();`

**Add BEFORE** that line:
```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IMessageBus, MessageBus>();
```

##### File 2: ShoppingCartAPI/Program.cs

**Check**: `builder.Services.AddHttpContextAccessor();` should already exist (around line 46)

If not present, add it before MessageBus registration.

**✅ Checkpoint**: Service Bus correlation ID propagation implemented

---

### Step 6: Update EmailAPI Service Bus Consumer (20 minutes)

Enable EmailAPI to read and use correlation IDs from Service Bus messages.

**File**: `Ecommerce.Services.EmailAPI/Messaging/AzureServiceBusConsumer.cs`

**Prerequisite**: Ensure Phase 2 completed:
- [ ] `ILogger<AzureServiceBusConsumer>` is injected in constructor
- [ ] All `Console.WriteLine` replaced with `_logger` calls

#### Step 6.1: Add Using Statement

At the top of the file, add:

```csharp
using Serilog.Context;
```

#### Step 6.2: Update OnEmailCartRequestReceived

Find the `OnEmailCartRequestReceived` method (around lines 60-80):

**BEFORE**:
```csharp
private async Task OnEmailCartRequestReceived(ProcessMessageEventArgs args)
{
    var message = args.Message;
    var body = Encoding.UTF8.GetString(message.Body);
    CartDto objMessage = JsonConvert.DeserializeObject<CartDto>(body);

    try
    {
        await _emailService.EmailCartAndLog(objMessage);
        await args.CompleteMessageAsync(message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing Service Bus message");
    }
}
```

**AFTER** (extract and use correlation ID):
```csharp
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

**What this does**:
1. Reads correlation ID from Service Bus message metadata
2. Wraps processing in `LogContext.PushProperty` scope
3. All logs automatically include the correlation ID
4. Matches correlation ID from the original request that published the message

#### Step 6.3: Update OnUserRegisterRequestReceived

Find the `OnUserRegisterRequestReceived` method (around lines 82-99):

**Apply the same pattern**:

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

**✅ Checkpoint**: EmailAPI consumer reads and propagates correlation IDs

---

## Testing Strategy

### Test 1: User Registration Flow (5 minutes)

**Path**: Web MVC → AuthAPI → Service Bus → EmailAPI

This tests the basic correlation flow through synchronous API calls and asynchronous messaging.

#### Setup
1. Open Visual Studio
2. Set startup projects to run all services:
   - Right-click Solution → **Set Startup Projects**
   - Select **Multiple startup projects**
   - Set all to **Start**: AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI, Web
   - Click **OK**
3. Press **F5** to start all services
4. Wait for all services to start (check output window)

#### Execution
1. Open **Web MVC**: https://localhost:7230
2. Click **Register**
3. Fill in form:
   - **Email**: `test1@example.com`
   - **Name**: `Test User One`
   - **Phone**: `1234567890`
   - **Password**: `Test123!`
4. Click **Register**
5. Wait for page to load (should redirect to login)

#### Verification
1. Open **Seq**: http://localhost:5341
2. In the search bar, type: `test1@example.com`
3. You should see logs from both **AuthAPI** and **EmailAPI**
4. Click on the first log entry
5. Look for **CorrelationId** field - copy the value (e.g., `a1b2c3d4-1234-5678-abcd-ef1234567890`)
6. Clear the search box
7. Paste the correlation ID into the search box
8. Click **Search** or press Enter

**Expected Logs** (in order):
```
[Web]     User registration initiated for test1@example.com
[AuthAPI] Registration attempt for user test1@example.com
[AuthAPI] User test1@example.com registered successfully
[AuthAPI] Publishing registration message to Service Bus
[EmailAPI] Received user registration message
[EmailAPI] User registration email logged successfully
```

**✅ Success**: All logs show the SAME CorrelationId

---

### Test 2: Shopping Cart Checkout Flow (5 minutes)

**Path**: Web → ShoppingCartAPI → ProductAPI + CouponAPI → Service Bus → EmailAPI

This tests correlation propagation across multiple downstream service calls.

#### Setup
1. All services still running from Test 1

#### Execution
1. Open **Web MVC**: https://localhost:7230
2. Click **Login**
3. Enter:
   - **Email**: `test1@example.com`
   - **Password**: `Test123!`
4. Click **Login**
5. Click on a product (e.g., "iPhone 14")
6. Click **Add to Cart**
7. Go to **Cart**
8. Enter coupon code: `10OFF` (or `20OFF`)
9. Click **Apply Coupon**
10. Click **Checkout**
11. Wait for page to load

#### Verification
1. Open **Seq**: http://localhost:5341
2. In search, type: `Checkout` or the cart total amount
3. Find a log from **ShoppingCartAPI** with action **CheckoutOrder**
4. Click on it and copy the **CorrelationId**
5. Clear search and paste the correlation ID

**Expected Logs** (from 6 services):
```
[Web]              Checkout initiated
[ShoppingCartAPI]  Checkout request received
[ShoppingCartAPI]  Calling ProductAPI to fetch product details
[ProductAPI]       GET /api/product/{id} called
[ProductAPI]       Product details retrieved
[ShoppingCartAPI]  Calling CouponAPI to validate coupon
[CouponAPI]        Coupon validation request received
[CouponAPI]        Coupon valid, discount calculated
[ShoppingCartAPI]  Publishing checkout message to Service Bus
[EmailAPI]         Received shopping cart email message
[EmailAPI]         Shopping cart email logged
```

**✅ Success**: All logs from 6 different services show the SAME CorrelationId

---

### Test 3: Direct API Call via Swagger (5 minutes)

**Path**: Swagger → ProductAPI only

This tests that API middleware generates correlation IDs for clients without upstream services.

#### Execution
1. Open **Swagger**: https://localhost:7000/swagger
2. Expand **GET /api/product**
3. Click **Try it out**
4. Click **Execute**
5. Look at the **Response headers** section
6. Find **x-correlation-id** header and copy the value

#### Verification
1. Open **Seq**: http://localhost:5341
2. Paste the correlation ID from the header into search
3. You should see logs from **ProductAPI** only

**Expected Logs**:
```
[ProductAPI] Fetching all products
[ProductAPI] Successfully retrieved 4 products
```

**✅ Success**: Response header contains X-Correlation-ID, and Seq shows the ID in logs

---

## Verification Checklist

After implementation, verify each item:

### Project Structure
- [ ] `E-commerce.Shared` project exists in solution
- [ ] `E-commerce.Shared/Middleware/CorrelationIdMiddleware.cs` exists
- [ ] `E-commerce.Shared` project builds without errors

### Project References
- [ ] AuthAPI references E-commerce.Shared
- [ ] ProductAPI references E-commerce.Shared
- [ ] CouponAPI references E-commerce.Shared
- [ ] ShoppingCartAPI references E-commerce.Shared
- [ ] EmailAPI references E-commerce.Shared
- [ ] Web references E-commerce.Shared

### Middleware Registration
- [ ] AuthAPI: `using Ecommerce.Shared.Middleware;` added
- [ ] AuthAPI: `app.UseCorrelationId();` added (after UseHttpsRedirection, before UseAuthentication)
- [ ] ProductAPI: middleware registered
- [ ] CouponAPI: middleware registered
- [ ] ShoppingCartAPI: middleware registered
- [ ] Web: middleware registered
- [ ] All services build without errors

### HTTP Client Handlers
- [ ] BackendAPIAuthenticationHttpClientHandler reads `CorrelationId` from HttpContext.Items
- [ ] BackendAPIAuthenticationHttpClientHandler adds X-Correlation-ID header
- [ ] BaseService has `IHttpContextAccessor` injected in constructor
- [ ] BaseService adds X-Correlation-ID header in SendAsync method
- [ ] BaseService sends correlation ID in all requests

### Service Bus
- [ ] Ecommerce.MessageBus has `IHttpContextAccessor` injected
- [ ] Ecommerce.MessageBus.PublishMessage uses correlation ID from context
- [ ] Ecommerce.MessageBus fallback chain implemented (HttpContext → Activity → GUID)
- [ ] AuthAPI registers `builder.Services.AddHttpContextAccessor();`
- [ ] ShoppingCartAPI verifies `builder.Services.AddHttpContextAccessor();` exists

### EmailAPI Consumer
- [ ] AzureServiceBusConsumer has `using Serilog.Context;`
- [ ] OnEmailCartRequestReceived wraps processing in LogContext.PushProperty scope
- [ ] OnUserRegisterRequestReceived wraps processing in LogContext.PushProperty scope
- [ ] Both methods extract correlationId from message.CorrelationId

### Build & Run
- [ ] Solution builds without errors
- [ ] All 6 projects build without errors
- [ ] AuthAPI starts without errors
- [ ] ProductAPI starts without errors
- [ ] CouponAPI starts without errors
- [ ] ShoppingCartAPI starts without errors
- [ ] EmailAPI starts without errors
- [ ] Web starts without errors

### Tests
- [ ] **Test 1 Passes**: User registration tracked across Web → AuthAPI → EmailAPI
- [ ] **Test 2 Passes**: Cart checkout tracked across all 6 services
- [ ] **Test 3 Passes**: Direct API call generates and returns correlation ID in response header

### Seq Verification
- [ ] Seq receives logs from all 6 services
- [ ] Logs include `CorrelationId` field (visible in detailed view)
- [ ] Searching by correlation ID shows related logs from all services
- [ ] Correlation ID format is a standard GUID (36 characters with hyphens)

---

## Troubleshooting

### Problem: Correlation ID not appearing in Seq logs

**Symptoms**:
- Seq shows logs but CorrelationId field is missing
- Logs don't show the structured property

**Root Causes** (check in order):

1. **Middleware not registered**
   - Check all 5 Program.cs files for `app.UseCorrelationId();`
   - Verify it's AFTER `UseHttpsRedirection` and BEFORE `UseAuthentication`

2. **Serilog.Context not enriching logs**
   - Check appsettings.json "Serilog" section
   - Verify "Enrich" includes `"FromLogContext"`
   - Verify log file template includes `{CorrelationId}`

3. **Middleware not registered in DI**
   - Check E-commerce.Shared project builds
   - Check all projects have project reference to E-commerce.Shared
   - Check `using Ecommerce.Shared.Middleware;` is in Program.cs

**Fix**:
```powershell
# 1. Clean solution
Right-click Solution → Clean Solution

# 2. Rebuild solution
Right-click Solution → Rebuild Solution

# 3. Restart services
Press F5
```

---

### Problem: Correlation ID lost in service-to-service calls

**Symptoms**:
- Web → AuthAPI has correlation ID ✓
- AuthAPI → other services has DIFFERENT correlation ID ✗
- ShoppingCartAPI → ProductAPI has DIFFERENT correlation ID ✗

**Root Causes**:

1. **HTTP client handler not propagating**
   - Check BackendAPIAuthenticationHttpClientHandler has the propagation code
   - Check BaseService has the propagation code

2. **CorrelationId not in HttpContext.Items**
   - Middleware not running
   - HttpContext not accessible to handler

3. **Null checks failing silently**
   - `TryGetValue` returns false (correlation ID not found)
   - Safe navigation operators (`?.`) returning null

**Fix**:
1. Add debug logging to handler:
   ```csharp
   _logger.LogDebug("Propagating correlation ID: {CorrelationId}", correlationId);
   ```

2. Verify middleware ran first by checking Seq:
   - Look for initial request log in Seq
   - Verify it has CorrelationId field

3. Rebuild and restart services

---

### Problem: EmailAPI has different Correlation ID than upstream services

**Symptoms**:
- Web → AuthAPI → EmailAPI flow has different IDs for each service
- Cannot search one ID to see complete flow

**Root Causes**:

1. **MessageBus not using correlation ID from context**
   - Check PublishMessage method has the correlation ID extraction code
   - Check it's not always generating new GUID

2. **IHttpContextAccessor not registered in publisher service**
   - AuthAPI: Needs `builder.Services.AddHttpContextAccessor();` before MessageBus registration
   - ShoppingCartAPI: Needs to have AddHttpContextAccessor registered

3. **EmailAPI consumer not reading correlation ID from message**
   - Check OnEmailCartRequestReceived wraps processing in LogContext.PushProperty
   - Check message.CorrelationId is being extracted

**Fix**:
1. **Add IHttpContextAccessor registration to AuthAPI Program.cs** (before MessageBus):
   ```csharp
   builder.Services.AddHttpContextAccessor();
   builder.Services.AddScoped<IMessageBus, MessageBus>();
   ```

2. **Verify MessageBus constructor has IHttpContextAccessor parameter**

3. **Verify PublishMessage uses the fallback chain correctly**:
   ```csharp
   var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
                       ?? Activity.Current?.GetBaggageItem("correlation_id")
                       ?? Guid.NewGuid().ToString();
   ```

4. Rebuild and restart

---

### Problem: Build error after adding packages

**Error**: `The type or namespace name 'Serilog' could not be found`

**Fix**:
1. Close Visual Studio
2. Delete `bin` and `obj` folders in the affected project
3. Delete `.vs` hidden folder in solution root
4. Reopen Visual Studio
5. Tools → NuGet Package Manager → Package Manager Console
6. Run: `Update-Package -Reinstall`
7. Rebuild solution

---

## Success Criteria

All of the following must be true:

✅ **One correlation ID tracks complete flow**
- User registers on Web
- Same correlation ID appears in AuthAPI logs
- Same correlation ID appears in EmailAPI logs (from Service Bus)

✅ **Correlation ID carries through API chains**
- Web → ShoppingCartAPI: Same correlation ID
- ShoppingCartAPI → ProductAPI: Same correlation ID
- ShoppingCartAPI → CouponAPI: Same correlation ID
- ShoppingCartAPI → Service Bus → EmailAPI: Same correlation ID

✅ **Seq search works**
- Search by correlation ID returns all related logs
- Logs are from multiple services
- Timeline shows request flow across services

✅ **Response headers include correlation ID**
- X-Correlation-ID header present in HTTP responses
- Value matches Seq logs

✅ **All tests pass**
- Test 1: User registration complete ✓
- Test 2: Cart checkout complete ✓
- Test 3: Direct API call complete ✓

✅ **No errors on startup**
- All 6 services start without exceptions
- Logs appear immediately in Seq and console

✅ **Web MVC tracking works** (CRITICAL)
- User actions on Web are tracked
- Correlation ID flows from Web to API calls
- Cannot track user flows without this

---

## Next Steps

After Phase 3 completion:

### Phase 4: OpenTelemetry Distributed Tracing
Add visual timing traces with Jaeger UI:
- See request flow as timeline
- Identify slow operations
- SQL query timing
- HTTP call timing

### Phase 5: Verification & Polish
- Document patterns for new developers
- Add dashboard for common queries
- Performance optimization based on traces

---

## Summary

**What You've Accomplished**:
✅ Created E-commerce.Shared project with correlation ID middleware
✅ Implemented correlation ID generation and propagation
✅ Enabled correlation tracking across HTTP calls
✅ Enabled correlation tracking through Service Bus messages
✅ Set up complete request tracing across 6 services

**You Can Now**:
- Debug production issues using one correlation ID
- Track user flows from Web through all APIs to email notifications
- Identify service bottlenecks with timing
- Search Seq for complete request journey

**Total Time**: ~2.5 hours for implementation + testing
