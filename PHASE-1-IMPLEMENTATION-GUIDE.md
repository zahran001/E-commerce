# Phase 1 Manual Implementation Guide: Add Serilog to AuthAPI

## 📋 Overview
This guide walks you through **Phase 1** of the Observability Implementation Guide: adding Serilog structured logging to the AuthAPI service. Follow each step carefully - this is a test implementation on a single service before rolling out to all 5 microservices.

**⏱️ Estimated Time:** 1 hour
**🎯 Goal:** Replace default ASP.NET Core logging with Serilog, logging to Console, File, and Seq
**✅ Prerequisites:**
- Visual Studio 2022 open with E-commerce solution loaded
- Seq container running (`docker ps | grep seq` shows it running)
- Internet connection (for NuGet package download)

---

## Current State Analysis

### What Exists
- ✅ Default ASP.NET Core logging configured in appsettings.json
- ✅ Basic logging configuration (Information level for Default, Warning for Microsoft.AspNetCore)
- ✅ User Secrets configured (ID: 92325206-97c9-412e-b374-fbe8fe1bf814)
- ✅ Health check endpoint with database connectivity check
- ✅ Service Bus message publishing (sends to `loguser` queue)
- ✅ Try-catch in AuthService.Register() but no logging

### What's Missing (Observability Gaps)
- ❌ NO ILogger injection in controllers or services
- ❌ NO structured logging framework (Serilog, NLog, etc.)
- ❌ NO file-based logging
- ❌ NO external log aggregation (Seq, Application Insights, etc.)
- ❌ NO correlation IDs
- ❌ NO exception logging
- ❌ NO performance/timing metrics

---

## Implementation Steps

### Step 1: Add Serilog NuGet Packages to AuthAPI

**Action:** Install 6 Serilog packages via NuGet Package Manager

**Packages to Install:**
1. `Serilog.AspNetCore` (latest 8.x)
2. `Serilog.Sinks.Console` (latest 5.x)
3. `Serilog.Sinks.File` (latest 5.x)
4. `Serilog.Sinks.Seq` (latest 7.x)
5. `Serilog.Enrichers.Environment` (latest 2.x)
6. `Serilog.Enrichers.Thread` (latest 3.x)

**File Modified:**
- `E-commerce.Services.AuthAPI\E-commerce.Services.AuthAPI.csproj`

**Verification:** Build project to ensure packages installed correctly

---

### Step 2: Configure Serilog in appsettings.json

**File:** `E-commerce.Services.AuthAPI\appsettings.json`

**Changes:**

1. **DELETE** existing `"Logging"` section (lines 2-7):
   ```json
   "Logging": {
     "LogLevel": {
       "Default": "Information",
       "Microsoft.AspNetCore": "Warning"
     }
   }
   ```

2. **ADD** new `"Serilog"` section at the top:
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
     "AllowedHosts": "*",
     "ConnectionStrings": { ... }
   }
   ```

**Key Configuration Decisions:**

- **Console Sink:** Human-readable format for development (HH:mm:ss timestamp)
- **File Sink:**
  - Daily rolling logs (`authapi-2025-12-20.log`)
  - Keep last 7 days of logs
  - Includes CorrelationId placeholder (for Phase 3)
- **Seq Sink:** Structured logs sent to http://localhost:5341
- **Log Levels:**
  - Application code: Information
  - Microsoft framework: Warning (reduces noise)
  - EF Core SQL commands: Warning (prevents SQL spam)
- **Enrichers:** Machine name and thread ID for debugging

---

### Step 3: Configure Serilog in Program.cs

**File:** `E-commerce.Services.AuthAPI\Program.cs`

**Changes:**

1. **Add using statement** at the top (after existing usings):
   ```csharp
   using Serilog;
   ```

2. **Add Serilog configuration** immediately after `var builder = WebApplication.CreateBuilder(args);` (line 8):
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

   **Why this approach:**
   - Reads configuration from appsettings.json (DRY principle)
   - Adds service-specific enrichers (Service, Environment)
   - Replaces default ASP.NET Core logging with Serilog

3. **Wrap application startup in try-catch-finally** (replace lines starting at `var app = builder.Build();`):

   **BEFORE:**
   ```csharp
   var app = builder.Build();

   // Configure the HTTP request pipeline.
   if (app.Environment.IsDevelopment())
   {
       app.UseSwagger();
       app.UseSwaggerUI();
   }

   app.UseCors("AllowAll");
   app.UseHttpsRedirection();
   app.UseAuthentication();
   app.UseAuthorization();
   app.MapControllers();

   app.MapGet("/health", async (ApplicationDbContext db) => { ... });

   if (!app.Environment.IsProduction())
   {
       ApplyMigration();
   }

   app.Run();
   ```

   **AFTER:**
   ```csharp
   var app = builder.Build();

   try
   {
       Log.Information("Starting AuthAPI service");

       // Configure the HTTP request pipeline.
       if (app.Environment.IsDevelopment())
       {
           app.UseSwagger();
           app.UseSwaggerUI();
       }

       app.UseCors("AllowAll");
       app.UseHttpsRedirection();
       app.UseAuthentication();
       app.UseAuthorization();
       app.MapControllers();

       app.MapGet("/health", async (ApplicationDbContext db) => { ... });

       if (!app.Environment.IsProduction())
       {
           ApplyMigration();
       }

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

   **Why this pattern:**
   - Logs startup message for debugging service initialization
   - Catches catastrophic failures that prevent service start
   - Ensures Serilog flushes all logs before shutdown (critical for file/Seq sinks)

#### Enhancement 1: Health Endpoint Logging

The health check endpoint should log exceptions for operational visibility:

```csharp
app.MapGet("/health", async (ApplicationDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        // ... success case ...
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Health check failed - database connectivity issue");
        return Results.Json(
            new { status = "unhealthy", service = "AuthAPI", timestamp = DateTime.UtcNow, error = ex.Message },
            statusCode: 503);
    }
});
```

**Why this matters:** Without logging, health check failures appear as silent timeouts in production, making debugging difficult. The Warning level indicates degraded service state without being a catastrophic Fatal error.

#### Enhancement 2: Database Migration Logging

The ApplyMigration() method should provide visibility into database state changes:

```csharp
void ApplyMigration()
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pendingMigrations = _db.Database.GetPendingMigrations().ToList();

            if (pendingMigrations.Count > 0)
            {
                Log.Information("Applying {MigrationCount} pending database migrations", pendingMigrations.Count);
                _db.Database.Migrate();
                Log.Information("Successfully applied all pending database migrations");
            }
            else
            {
                Log.Information("No pending database migrations to apply");
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error applying database migrations - service startup failed");
            throw;
        }
    }
}
```

**Why this matters:** Database migrations are critical to application startup. Silent migration failures lead to cryptic runtime errors later (e.g., "table doesn't exist"). Logging migration count and success provides:
- **Startup visibility:** See exactly what schema changes occurred on service initialization
- **Debugging aid:** If migrations fail, Fatal log shows the exact error
- **Audit trail:** Track when database schema evolved across environments

---

### Step 4: Add ILogger to AuthAPIController

**File:** `E-commerce.Services.AuthAPI\Controllers\AuthAPIController.cs`

**Changes:**

1. **Add ILogger field** (line ~15, after existing fields):
   ```csharp
   private readonly IAuthService _authService;
   private readonly ILogger<AuthAPIController> _logger;  // NEW
   protected ResponseDto _response;
   private readonly IMessageBus _messageBus;
   private IConfiguration _configuration;
   ```

2. **Update constructor** to inject ILogger (line ~20):

   **BEFORE:**
   ```csharp
   public AuthAPIController(IAuthService authService, IMessageBus messageBus, IConfiguration configuration)
   {
       _authService = authService;
       _response = new();
       _messageBus = messageBus;
       _configuration = configuration;
   }
   ```

   **AFTER:**
   ```csharp
   public AuthAPIController(
       IAuthService authService,
       ILogger<AuthAPIController> logger,  // NEW
       IMessageBus messageBus,
       IConfiguration configuration)
   {
       _authService = authService;
       _logger = logger;  // NEW
       _response = new();
       _messageBus = messageBus;
       _configuration = configuration;
   }
   ```

3. **Add logging to Register endpoint** (line ~29):

   **BEFORE:**
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

       await _messageBus.PublishMessage(model.Email,
           _configuration.GetValue<string>("TopicAndQueueNames:LogUserQueue"));

       return Ok(_response);
   }
   ```

   **AFTER:**
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

       await _messageBus.PublishMessage(model.Email,
           _configuration.GetValue<string>("TopicAndQueueNames:LogUserQueue"));

       return Ok(_response);
   }
   ```

   **Logging Strategy:**
   - **Entry point:** LogInformation on registration attempt (captures intent)
   - **Failure:** LogWarning with error details (not Error, since validation failures are expected)
   - **Success:** LogInformation on successful registration
   - **Structured logging:** Use `{Email}` placeholder (not string interpolation) for Seq filtering

---

## Files Modified Summary

| File | Changes | Lines Modified |
|------|---------|----------------|
| `E-commerce.Services.AuthAPI.csproj` | Add 6 Serilog NuGet packages | PackageReference items |
| `appsettings.json` | Replace Logging section with Serilog configuration | Delete 6 lines, Add ~40 lines |
| `Program.cs` | Add Serilog setup, wrap app.Run() in try-catch-finally | Add ~25 lines |
| `Controllers\AuthAPIController.cs` | Add ILogger field, inject in constructor, add 3 logging calls | Add ~10 lines |

**Total:** 4 files modified, ~75 lines added, ~6 lines deleted

---

## Rollback Plan

If issues occur during implementation:

1. **Remove NuGet packages** via Package Manager
2. **Revert appsettings.json** to original Logging configuration
3. **Remove Serilog code** from Program.cs
4. **Remove ILogger injection** from AuthAPIController.cs

**Git safety:** Commit working state before starting Phase 1

---

## Next Steps (Not Part of Phase 1)

After Phase 1 verification:
- **Phase 2:** Roll out Serilog to remaining 4 services (ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI)
- **Phase 3:** Add Correlation ID middleware
- **Phase 4:** Add OpenTelemetry distributed tracing
- **Phase 5:** End-to-end testing across all services

---

## Key Decisions & Rationale

### Why Start with AuthAPI?
- **Simplest service** for testing (just register/login operations)
- **Easy to verify:** Single endpoint test (register user)
- **Critical path:** Authentication is first service users interact with
- **Low risk:** Minimal dependencies on other services (only publishes to EmailAPI)

### Why These Log Levels?
- **Information for app code:** Captures business events (registration, login)
- **Warning for Microsoft:** Reduces framework noise (request routing, middleware execution)
- **Warning for EF Core:** Prevents SQL query spam (hundreds of lines per request)

### Why 3 Sinks?
- **Console:** Immediate feedback during development
- **File:** Persistent logs for debugging (survives app restarts)
- **Seq:** Structured querying and filtering (production-ready)

### How Do Logs Get to Seq?

**The Flow:**
1. **Application logs with ILogger:** `_logger.LogInformation("User {Email} registered", email)`
2. **Serilog intercepts the log call** (via `builder.Host.UseSerilog()` in Program.cs)
3. **Serilog reads configuration** from appsettings.json and finds 3 WriteTo sinks
4. **For Seq sink specifically:**
   - `Serilog.Sinks.Seq` NuGet package provides HTTP client
   - Sends JSON payload to `http://localhost:5341/api/events/raw` (Seq ingestion endpoint)
   - Uses HTTP POST with structured log data
5. **Seq receives and stores** the log event in its internal database
6. **Seq dashboard displays** logs in real-time UI

**Configuration Magic:**
```json
{
  "Name": "Seq",
  "Args": {
    "serverUrl": "http://localhost:5341"  // Seq HTTP API endpoint
  }
}
```

This tells Serilog.Sinks.Seq to:
- Connect to Seq running at localhost:5341
- Send all log events via HTTP
- Use Seq's ingestion API at `/api/events/raw`
- Automatically batch logs for efficiency

**Network Requirements:**
- Seq container must be running: `docker ps | grep seq`
- Port 5341 must be accessible from AuthAPI
- No authentication required (we disabled it with `SEQ_FIRSTRUN_NOAUTHENTICATION=true`)

**Fallback Behavior:**
If Seq is unavailable:
- Serilog logs a warning to Console: "Unable to connect to Seq"
- Logs still go to Console and File sinks
- Application continues running normally
- When Seq comes back online, new logs automatically flow to it

### Why Structured Logging?
- **Filterable properties:** Search Seq by email, user ID, etc.
- **Aggregation:** Count failed registrations by error type
- **Correlation:** Link logs across services (Phase 3)
- **Alerting:** Trigger alerts on specific patterns

---

## Risk Assessment

### Low Risk
- ✅ Serilog is production-proven (used by thousands of .NET applications)
- ✅ No breaking changes to existing code (additive only)
- ✅ Falls back to default logging if Serilog fails to initialize

### Medium Risk
- ⚠️ **Seq unavailable:** If Seq container not running, logs still go to Console/File
- ⚠️ **Disk space:** File logs could fill disk (mitigated by 7-day retention)
- ⚠️ **Performance:** Minimal impact (<1ms per log call), but excessive logging could slow requests

### Mitigation
- Keep Seq running via `docker start seq`
- Monitor disk space in production
- Use Information level (not Debug/Verbose) for high-frequency code paths

---

## Post-Implementation Validation

After completing all steps:

1. ✅ Build succeeds with no errors
2. ✅ AuthAPI starts without exceptions
3. ✅ Startup log appears: "Starting AuthAPI service"
4. ✅ Swagger UI loads successfully
5. ✅ Register endpoint returns 200 OK for valid request
6. ✅ Logs appear in all 3 locations (Console, File, Seq)
7. ✅ Email property is searchable in Seq
8. ✅ Service property = "AuthAPI" in Seq
9. ✅ Log files rotate daily
10. ✅ No Console.WriteLine statements remain

**Estimated Implementation Time:** 45-60 minutes
**Estimated Testing Time:** 15 minutes
**Total Phase 1 Time:** ~1 hour

---

---

# 📖 MANUAL IMPLEMENTATION GUIDE - STEP BY STEP

Follow these instructions exactly. Each step includes verification checkpoints.

---

## STEP 1: Install Serilog NuGet Packages

### 1.1 Open NuGet Package Manager

1. In **Solution Explorer**, locate `E-commerce.Services.AuthAPI`
2. **Right-click** on the project
3. Select **"Manage NuGet Packages..."**
4. The NuGet Package Manager window opens

### 1.2 Install Packages One by One

Click the **"Browse"** tab at the top.

**Package 1: Serilog.AspNetCore**
1. Type `Serilog.AspNetCore` in the search box
2. Click on **Serilog.AspNetCore** in the results (by Serilog)
3. On the right side, verify version is **8.0.0 or newer**
4. Click **Install**
5. Click **OK** when prompted about changes
6. Click **I Accept** for license agreement
7. Wait for green checkmark: "Finished" in the output window

**Package 2: Serilog.Sinks.Console**
1. Clear search box, type `Serilog.Sinks.Console`
2. Click on **Serilog.Sinks.Console** (by Serilog)
3. Verify version **5.0.1 or newer**
4. Click **Install**
5. Accept prompts

**Package 3: Serilog.Sinks.File**
1. Search for `Serilog.Sinks.File`
2. Click on **Serilog.Sinks.File**
3. Verify version **5.0.0 or newer**
4. Click **Install**
5. Accept prompts

**Package 4: Serilog.Sinks.Seq**
1. Search for `Serilog.Sinks.Seq`
2. Click on **Serilog.Sinks.Seq**
3. Verify version **7.0.1 or newer**
4. Click **Install**
5. Accept prompts

**Package 5: Serilog.Enrichers.Environment**
1. Search for `Serilog.Enrichers.Environment`
2. Click on **Serilog.Enrichers.Environment**
3. Verify version **2.3.0 or newer**
4. Click **Install**
5. Accept prompts

**Package 6: Serilog.Enrichers.Thread**
1. Search for `Serilog.Enrichers.Thread`
2. Click on **Serilog.Enrichers.Thread**
3. Verify version **3.2.0 or newer**
4. Click **Install**
5. Accept prompts

### 1.3 Verify Installation

1. Click the **"Installed"** tab in NuGet Package Manager
2. You should see all 6 packages listed:
   - ✅ Serilog.AspNetCore
   - ✅ Serilog.Sinks.Console
   - ✅ Serilog.Sinks.File
   - ✅ Serilog.Sinks.Seq
   - ✅ Serilog.Enrichers.Environment
   - ✅ Serilog.Enrichers.Thread

3. **Close the NuGet Package Manager window**

### 1.4 Build to Verify

1. In Solution Explorer, **right-click** on `E-commerce.Services.AuthAPI`
2. Click **"Build"**
3. Check **Output window** (View → Output)
4. Should see: **"Build succeeded"** (0 errors)

✅ **Checkpoint:** All 6 packages installed, project builds successfully

---

## STEP 2: Update appsettings.json

### 2.1 Open appsettings.json

1. In Solution Explorer, expand `E-commerce.Services.AuthAPI`
2. **Double-click** on `appsettings.json`
3. File opens in editor

### 2.2 Delete Old Logging Section

**Find these lines** (should be at the top, lines 2-7):
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  }
},
```

**Select all 5 lines** (including the comma after the closing brace)
**Press Delete**

### 2.3 Add Serilog Configuration

**Place your cursor** right after the opening `{` on line 1, press Enter to create a new line.

**Copy and paste** this entire block:

```json
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
```

**Important:** Make sure there's a **comma** after the closing `}` of Serilog section (before "AllowedHosts")

### 2.4 Verify JSON Format

1. Click anywhere in the JSON file
2. Press **Ctrl+K, Ctrl+D** (format document)
3. If you see errors, check for:
   - Missing commas between sections
   - Unmatched braces `{}`
   - Missing quotes around property names

### 2.5 Save File

Press **Ctrl+S** to save

✅ **Checkpoint:** appsettings.json has Serilog section, no red squiggly lines

---

## STEP 3: Update Program.cs

### 3.1 Open Program.cs

1. In Solution Explorer, under `E-commerce.Services.AuthAPI`
2. **Double-click** on `Program.cs`

### 3.2 Add Using Statement

**At the top of the file**, after the existing `using` statements (around line 5), add:

```csharp
using Serilog;
```

Your using block should now look like:
```csharp
using E_commerce.Services.AuthAPI.Data;
using E_commerce.Services.AuthAPI.Models;
using E_commerce.Services.AuthAPI.Service;
using E_commerce.Services.AuthAPI.Service.IService;
using Ecommerce.MessageBus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;  // ← NEW LINE
```

### 3.3 Add Serilog Configuration

**Find this line** (should be around line 8):
```csharp
var builder = WebApplication.CreateBuilder(args);
```

**Immediately after that line**, press Enter and add this code block:

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

### 3.4 Wrap app.Run() in Try-Catch-Finally

**Find the line** (around line 60):
```csharp
var app = builder.Build();
```

**Select everything from** `var app = builder.Build();` **down to** `app.Run();` (the last line before the `ApplyMigration()` method)

**Replace with:**

```csharp
var app = builder.Build();

try
{
    Log.Information("Starting AuthAPI service");

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("AllowAll");
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Health check endpoint
    app.MapGet("/health", async (ApplicationDbContext db) =>
    {
        try
        {
            // Test database connectivity
            var canConnect = await db.Database.CanConnectAsync();

            if (canConnect)
            {
                return Results.Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    service = "AuthAPI",
                    database = "connected"
                });
            }
            else
            {
                return Results.Json(
                    new
                    {
                        status = "unhealthy",
                        timestamp = DateTime.UtcNow,
                        service = "AuthAPI",
                        database = "disconnected"
                    },
                    statusCode: 503
                );
            }
        }
        catch (Exception ex)
        {
            return Results.Json(
                new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    service = "AuthAPI",
                    error = ex.Message
                },
                statusCode: 503
            );
        }
    });

    if (!app.Environment.IsProduction())
    {
        ApplyMigration();
    }

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

### 3.5 Save and Build

1. Press **Ctrl+S** to save
2. **Build → Build E-commerce.Services.AuthAPI**
3. Check Output window: Should see **"Build succeeded"**

✅ **Checkpoint:** Program.cs builds with no errors

---

## STEP 4: Update AuthAPIController.cs

### 4.1 Open AuthAPIController.cs

1. In Solution Explorer, expand `E-commerce.Services.AuthAPI` → `Controllers`
2. **Double-click** on `AuthAPIController.cs`

### 4.2 Add ILogger Field

**Find the private fields** at the top of the class (around line 15):

```csharp
private readonly IAuthService _authService;
protected ResponseDto _response;
private readonly IMessageBus _messageBus;
private IConfiguration _configuration;
```

**Add this line** after `_authService`:

```csharp
private readonly IAuthService _authService;
private readonly ILogger<AuthAPIController> _logger;  // ← ADD THIS LINE
protected ResponseDto _response;
private readonly IMessageBus _messageBus;
private IConfiguration _configuration;
```

### 4.3 Update Constructor

**Find the constructor** (around line 20):

```csharp
public AuthAPIController(IAuthService authService, IMessageBus messageBus, IConfiguration configuration)
{
    _authService = authService;
    _response = new();
    _messageBus = messageBus;
    _configuration = configuration;
}
```

**Replace with:**

```csharp
public AuthAPIController(
    IAuthService authService,
    ILogger<AuthAPIController> logger,  // ← ADD THIS PARAMETER
    IMessageBus messageBus,
    IConfiguration configuration)
{
    _authService = authService;
    _logger = logger;  // ← ADD THIS LINE
    _response = new();
    _messageBus = messageBus;
    _configuration = configuration;
}
```

### 4.4 Add Logging to Register Method

**Find the Register method** (around line 30):

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

    await _messageBus.PublishMessage(model.Email,
        _configuration.GetValue<string>("TopicAndQueueNames:LogUserQueue"));

    return Ok(_response);
}
```

**Replace with:**

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

    await _messageBus.PublishMessage(model.Email,
        _configuration.GetValue<string>("TopicAndQueueNames:LogUserQueue"));

    return Ok(_response);
}
```

### 4.5 Save and Build

1. Press **Ctrl+S** to save
2. **Build → Build Solution** (or Ctrl+Shift+B)
3. Check Output window: **"Build succeeded"** (0 errors)

✅ **Checkpoint:** AuthAPIController builds successfully, no red squiggly lines

---

## STEP 5: Test Phase 1 Implementation

### 5.1 Set AuthAPI as Startup Project

1. In Solution Explorer, **right-click** on `E-commerce.Services.AuthAPI`
2. Select **"Set as Startup Project"**
3. Project name becomes bold

### 5.2 Start Debugging

1. Press **F5** (or click green "Play" button)
2. Wait for service to start

### 5.3 Verify Console Output

In the **Visual Studio Output window** (View → Output), you should see:

```
[10:23:45 INF] Starting AuthAPI service
[10:23:46 INF] Microsoft.Hosting.Lifetime | Now listening on: https://localhost:7002
[10:23:46 INF] Microsoft.Hosting.Lifetime | Application started. Press Ctrl+C to shut down.
```

✅ **Checkpoint:** Colored log output in console with timestamps

### 5.4 Verify Seq Dashboard

1. Open browser
2. Go to: **http://localhost:5341**
3. You should see Seq dashboard
4. Click **"Events"** in the left sidebar
5. You should see logs from AuthAPI:
   - "Starting AuthAPI service"
   - "Now listening on..."
   - "Application started"

6. Click on any log entry to expand it
7. Verify it has these properties:
   - `Service`: "AuthAPI"
   - `Environment`: "Development"
   - `MachineName`: (your computer name)
   - `ThreadId`: (a number)

✅ **Checkpoint:** Logs visible in Seq with structured properties

### 5.5 Test Registration Endpoint via Swagger

1. In browser, go to: **https://localhost:7002/swagger**
2. Expand **POST /api/auth/register**
3. Click **"Try it out"**
4. In the Request body, enter:

```json
{
  "email": "testuser@example.com",
  "firstName": "Test",
  "lastName": "User",
  "phoneNumber": "1234567890",
  "password": "Test123!",
  "role": null
}
```

5. Click **"Execute"**
6. Scroll down to "Response" section
7. Should see **200 OK** status code
8. Response body should show:
```json
{
  "result": null,
  "isSuccess": true,
  "message": ""
}
```

### 5.6 Verify Logs in Seq

1. Go back to Seq dashboard (http://localhost:5341)
2. You should see **3 new log entries**:
   - `[INF] Registration attempt for user testuser@example.com`
   - `[INF] User testuser@example.com registered successfully`
   - (Possibly) Service Bus message log

3. Click on the **"Registration attempt"** log
4. Expand to see properties
5. Verify `Email` property = "testuser@example.com"

6. In Seq search bar, type: `testuser@example.com`
7. Press Enter
8. Should see only logs related to that email

✅ **Checkpoint:** Registration logs appear in Seq with searchable Email property

### 5.7 Test Failure Scenario

1. Go back to Swagger
2. Try to register the **same user again**:
   - Use same JSON from step 5.5
   - Click "Execute"

3. Should see **400 Bad Request**
4. Response body shows error message (user already exists)

5. Go to Seq
6. Search for: `testuser@example.com`
7. You should see **new WARNING log**:
   - `[WRN] Registration failed for testuser@example.com: User 'testuser@example.com' already exists.`

✅ **Checkpoint:** Failed registration logged as Warning

### 5.8 Verify File Logs

1. **Stop debugging** (Shift+F5)
2. In Windows Explorer, navigate to:
   ```
   C:\Users\minha\source\repos\E-commerce\E-commerce.Services.AuthAPI\logs\
   ```

3. You should see a file named: `authapi-2025-12-20.log` (today's date)
4. Open it with Notepad
5. Should see structured log entries:
   ```
   2025-12-20 10:23:45.123 +03:00 [INF] E_commerce.Services.AuthAPI.Program | AuthAPI |  | Starting AuthAPI service
   2025-12-20 10:25:30.456 +03:00 [INF] E_commerce.Services.AuthAPI.Controllers.AuthAPIController | AuthAPI |  | Registration attempt for user testuser@example.com
   ```

✅ **Checkpoint:** Log file created with structured format

---

## ✅ Phase 1 Complete - Verification Checklist

Check all items before proceeding to Phase 2:

- [ ] All 6 Serilog NuGet packages installed
- [ ] appsettings.json has Serilog section (Logging section removed)
- [ ] Program.cs imports Serilog and configures it
- [ ] Try-catch-finally wraps app.Run() in Program.cs
- [ ] AuthAPIController has ILogger injected
- [ ] Register method has 3 logging calls
- [ ] Solution builds with 0 errors
- [ ] AuthAPI starts successfully (F5)
- [ ] Console shows colored log output with timestamps
- [ ] Seq dashboard (localhost:5341) shows logs
- [ ] Logs in Seq have Service="AuthAPI" property
- [ ] Swagger registration works (200 OK)
- [ ] Registration logs appear in Seq
- [ ] Email property is searchable in Seq
- [ ] Duplicate registration shows Warning in Seq
- [ ] Log file created in logs/ folder
- [ ] No Console.WriteLine statements remain

---

## 🐛 Troubleshooting

### Problem: Build Errors After Adding Packages

**Error:** "Could not find package Serilog..."

**Solution:**
1. Tools → NuGet Package Manager → Package Manager Settings
2. Package Sources → Click "+" button
3. Name: nuget.org
4. Source: https://api.nuget.org/v3/index.json
5. Click Update, then OK
6. Rebuild solution

---

### Problem: Red Squiggly Lines Under "Serilog"

**Error:** "The type or namespace name 'Serilog' could not be found"

**Solution:**
1. Right-click project → Manage NuGet Packages
2. Click "Installed" tab
3. Verify Serilog.AspNetCore is listed
4. If not, reinstall it from Browse tab
5. Clean solution (Build → Clean Solution)
6. Rebuild (Ctrl+Shift+B)

---

### Problem: No Logs Appear in Seq

**Check 1: Is Seq running?**
```powershell
docker ps | grep seq
```
Should show: `seq   Up X minutes   0.0.0.0:5341->80/tcp`

If not running:
```powershell
docker start seq
```

**Check 2: Can you access Seq dashboard?**
Open http://localhost:5341 in browser
- If page doesn't load, Seq container is not running
- Restart: `docker restart seq`

**Check 3: Are logs going to Console?**
- If Console shows logs but Seq doesn't, check appsettings.json
- Verify "serverUrl": "http://localhost:5341" (not https)

**Check 4: Firewall blocking?**
- Temporarily disable Windows Firewall
- Test again

---

### Problem: Application Won't Start (Exception on Launch)

**Error:** "No default admin password was supplied"

**Wrong container config - Restart Seq:**
```powershell
docker stop seq
docker rm seq
docker run -d --name seq -e ACCEPT_EULA=Y -e SEQ_FIRSTRUN_NOAUTHENTICATION=true -p 5341:80 datalust/seq:latest
```

---

### Problem: JSON Syntax Error in appsettings.json

**Error:** "Unexpected token ..."

**Solution:**
1. In Visual Studio, open appsettings.json
2. Press Ctrl+K, Ctrl+D (format document)
3. Look for:
   - Missing commas between sections
   - Extra commas after last item in array
   - Unmatched `{` or `[` brackets
   - Missing quotes around property names

**Common mistakes:**
```json
// ❌ WRONG - extra comma after last item
"Enrich": ["FromLogContext", "WithMachineName",]

// ✅ CORRECT
"Enrich": ["FromLogContext", "WithMachineName"]
```

---

### Problem: Logs Folder Not Created

**Expected:** `E-commerce.Services.AuthAPI\logs\authapi-YYYY-MM-DD.log`

**Solution:**
- Folder is auto-created on first log write
- Run the application (F5)
- Make at least one request (Swagger test)
- Stop application (Shift+F5)
- Check folder again - should exist now

---

## 📊 What You've Accomplished

Congratulations! You've successfully:

✅ Added Serilog structured logging to AuthAPI
✅ Configured 3 log sinks (Console, File, Seq)
✅ Added logging to the Register endpoint
✅ Verified logs appear in all 3 locations
✅ Tested searchable properties in Seq
✅ Created a foundation for observability

**Next:** Phase 2 - Roll out Serilog to the other 4 services (ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI)
