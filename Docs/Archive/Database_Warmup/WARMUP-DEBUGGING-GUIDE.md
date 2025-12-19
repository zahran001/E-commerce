# Database Warm-Up Debugging Guide

Since only ProductAPI's database is warming up, but Auth, Coupon, and ShoppingCart databases aren't, here's a systematic approach to debug the issue.

## Quick Diagnosis Checklist

- [ ] All APIs are actually running
- [ ] Health endpoints are returning 200 OK responses
- [ ] Health endpoints are actually executing database checks
- [ ] Web MVC is able to reach the APIs (network/firewall issue)
- [ ] Configuration is correctly loaded in Web MVC
- [ ] Hosted service is actually executing

## Step 1: Verify All APIs are Running and Healthy

Run this to check each health endpoint directly:

```powershell
# ProductAPI (should work since DB is warming up)
Invoke-WebRequest -Uri "https://localhost:7000/health" -SkipCertificateCheck

# CouponAPI (likely failing)
Invoke-WebRequest -Uri "https://localhost:7001/health" -SkipCertificateCheck

# AuthAPI (likely failing)
Invoke-WebRequest -Uri "https://localhost:7002/health" -SkipCertificateCheck

# ShoppingCartAPI (likely failing)
Invoke-WebRequest -Uri "https://localhost:7003/health" -SkipCertificateCheck
```

**Expected Output:**
```json
{
  "status": "healthy",
  "service": "CouponAPI",
  "timestamp": "2025-12-13T...",
  "database": "connected"
}
```

**If any return 500 or timeout:**
- The API is running but health endpoint is crashing
- Check API console logs for exceptions
- Likely: database connection issue, DbContext not registered

---

## Step 2: Check Web MVC Console/Logs During Startup

Start Web MVC and look for these log messages within the first 10 seconds:

```
info: E_commerce.Web.Services.DatabaseWarmUpHostedService
      Starting database warm-up for 4 services
info: E_commerce.Web.Services.DatabaseWarmUpHostedService
      Warming up ProductAPI at https://localhost:7000/health
info: E_commerce.Web.Services.DatabaseWarmUpHostedService
      Warming up CouponAPI at https://localhost:7001/health
info: E_commerce.Web.Services.DatabaseWarmUpHostedService
      Warming up AuthAPI at https://localhost:7002/health
info: E_commerce.Web.Services.DatabaseWarmUpHostedService
      Warming up ShoppingCartAPI at https://localhost:7003/health
```

**If you DON'T see these messages:**
- The hosted service isn't running
- Possible causes:
  1. Configuration isn't bound correctly
  2. Service isn't registered in DI
  3. Configuration is disabled (`"Enabled": false`)

**If you see the "Warming up X" messages but services time out or fail:**
- The hosted service is running but can't reach the APIs
- Check if all APIs are actually started before Web MVC

---

## Step 3: Debug Configuration Loading

Add temporary logging to Web MVC `Program.cs` to verify configuration is loaded correctly:

```csharp
// After building the app, add this (before app.Run()):
var config = app.Services.GetRequiredService<IOptions<WarmUpConfiguration>>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("WarmUp Config - Enabled: {Enabled}, Services: {Count}",
    config.Value.Enabled,
    config.Value.Services.Count);

foreach (var service in config.Value.Services)
{
    logger.LogInformation("  - {Name}: {Url}{Endpoint}",
        service.Name,
        service.BaseUrl,
        service.HealthEndpoint);
}
```

This will tell you:
1. If configuration section is being read
2. If all 4 services are configured
3. If URLs are correct

---

## Step 4: Verify Database Context Dependency Injection

The health endpoints use `ApplicationDbContext db` from dependency injection. If the DbContext isn't registered, it will fail.

Check each API's `Program.cs` to ensure this line exists **BEFORE** `var app = builder.Build();`:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});
```

**Common Issue:** If DbContext is registered AFTER `app.Build()`, the health endpoint injection will fail.

---

## Step 5: Test Health Endpoints in Isolation

Create a simple test script to understand why health endpoints might be failing:

**Create a test file: `test-health.csx`**

```csharp
using System.Net;

// Bypass certificate validation for localhost
ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

var endpoints = new[]
{
    ("ProductAPI", "https://localhost:7000/health"),
    ("CouponAPI", "https://localhost:7001/health"),
    ("AuthAPI", "https://localhost:7002/health"),
    ("ShoppingCartAPI", "https://localhost:7003/health"),
};

foreach (var (name, url) in endpoints)
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"{name}: {response.StatusCode}");
        Console.WriteLine($"  Response: {content}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{name}: ERROR - {ex.GetType().Name}: {ex.Message}");
    }
}
```

Run with: `dotnet script test-health.csx`

**Interpretation:**
- **200 OK**: API and DB are working
- **503 Service Unavailable**: Health endpoint ran but DB connection failed
- **500 Internal Server Error**: Exception in health endpoint code
- **Connection timeout/refused**: API isn't running or listening on that port
- **HttpRequestException**: Network issue

---

## Step 6: Check Database Connection Strings

Each API needs a valid connection string. In SQL Server Management Studio, verify:

1. **Databases exist:**
   - `E-commerce_Auth`
   - `E-commerce_Product` (this one works)
   - `E-commerce_Coupon`
   - `E-commerce_ShoppingCart`
   - `E-commerce_Email`

2. **Each database is accessible** from your machine

3. **Each API's appsettings.json has correct connection string:**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=ZAHRAN;Database=E-commerce_CouponAPI;Trusted_Connection=True;TrustServerCertificate=True"
     }
   }
   ```

---

## Step 7: Compare Product vs. Other APIs

Since ProductAPI's database warms up successfully, compare it to AuthAPI to find the difference:

### ProductAPI - Program.cs
```csharp
// Check these sections exist:
builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

app.MapGet("/health", async (ApplicationDbContext db) =>
{
    // database-aware health check
});
```

### AuthAPI - Program.cs
```csharp
// Verify IDENTICAL structure
```

**Key differences to look for:**
- Different DbContext class name?
- Different connection string key?
- Health endpoint not injecting DbContext?
- Health endpoint not being mapped?
- DbContext registered AFTER app.Build()?

---

## Step 8: Enable Debug Logging

Update `appsettings.Development.json` to see more details:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Connection": "Information",
      "E_commerce.Web.Services.DatabaseWarmUpHostedService": "Debug"
    }
  }
}
```

This will show:
- When DbContext is created/disposed
- When database connections are attempted
- When queries execute

---

## Most Likely Causes (Ranked by Probability)

### 1. **DbContext Not Injected in Health Endpoint (Most Likely)**
If the health endpoint doesn't have `async (ApplicationDbContext db) =>`, it won't touch the database.

**Check:** Does the health endpoint in AuthAPI/CouponAPI/ShoppingCartAPI have the parameter?

**Fix:** Add it if missing:
```csharp
app.MapGet("/health", async (ApplicationDbContext db) =>  // <-- This parameter is critical
{
    var canConnect = await db.Database.CanConnectAsync();
    // ...
});
```

### 2. **Services Not Running**
The warm-up service tries to call the APIs, but they're not started.

**Check:** Are all 4 API processes running in Visual Studio?

**Fix:** Start all APIs before Web MVC

### 3. **Configuration Disabled**
```json
"DatabaseWarmUp": {
  "Enabled": false  // <-- This disables it
}
```

**Check:** Is `"Enabled": true` in appsettings.json?

### 4. **Configuration Not Found**
The configuration section doesn't exist or has wrong name.

**Check:** Verify section name is exactly `"DatabaseWarmUp"`

### 5. **Network/Firewall**
APIs are running but Web MVC can't reach them.

**Check:** Can you manually curl/invoke the health endpoints from a separate terminal?

---

## Debugging Workflow

1. **Start ALL 4 APIs** (ProductAPI, CouponAPI, AuthAPI, ShoppingCartAPI)
2. **Manually test each health endpoint** from PowerShell
3. **Check Web MVC logs** for warm-up messages
4. **Compare successful (Product) vs. failing (Auth/Coupon/Cart) implementations**
5. **Add temporary logging** to understand the flow
6. **Check database connectivity** from each API
7. **Verify configuration** is being loaded

---

## What to Look For in Logs

**Success indicators:**
```
Starting database warm-up for 4 services
Warming up AuthAPI at https://localhost:7002/health
AuthAPI is ready (attempt 1/3, 1234ms)
```

**Failure indicators:**
```
Starting database warm-up for 4 services
Warming up AuthAPI at https://localhost:7002/health
AuthAPI timed out after 45000ms on attempt 1/3
AuthAPI failed on attempt 2/3: Connection refused
AuthAPI failed to warm up after 3 attempts
```

**Missing warm-up entirely:**
```
[No "Starting database warm-up" message in logs]
```
This means the hosted service didn't execute or is disabled.

---

## Quick Fixes to Try

### Fix 1: Ensure Health Endpoints Are Database-Aware

Check all 4 APIs have this pattern in Program.cs:

```csharp
app.MapGet("/health", async (ApplicationDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        if (!canConnect)
        {
            return Results.Json(
                new { status = "unhealthy", service = "AuthAPI", timestamp = DateTime.UtcNow, database = "disconnected" },
                statusCode: 503);
        }
        return Results.Ok(new {
            status = "healthy",
            service = "AuthAPI",
            timestamp = DateTime.UtcNow,
            database = "connected"
        });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "AuthAPI", timestamp = DateTime.UtcNow, error = ex.Message },
            statusCode: 503);
    }
});
```

**If health endpoint is missing the `ApplicationDbContext db` parameter:**
- It won't trigger database wake-up
- It will return 200 OK without connecting to database
- Azure SQL database will stay paused

### Fix 2: Verify Service Registration Order

In Program.cs, DbContext must be registered BEFORE `app.Build()`:

```csharp
// CORRECT ORDER:
builder.Services.AddDbContext<ApplicationDbContext>(...);  // First
var app = builder.Build();  // Then build

// WRONG ORDER (DbContext registered after app.Build()):
var app = builder.Build();
builder.Services.AddDbContext<ApplicationDbContext>(...);  // Too late!
```

### Fix 3: Check Configuration File

Verify `appsettings.json` has the exact section:

```json
"DatabaseWarmUp": {
  "Enabled": true,
  "Services": [ ... ]
}
```

Not `"DatabaseWarmup"` or `"DB_WARMUP"` or other variations.

---

## Next Steps

1. Run the health endpoint tests (Step 1)
2. Check the logs (Step 2)
3. Compare configurations (Step 7)
4. Apply the fixes above if issues found
5. Report what you find - this will pinpoint the exact cause
