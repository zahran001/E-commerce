# Database Warm-Up Service Implementation Plan

## Problem Statement

When the Web MVC application starts in Azure Container Apps, only the Product database wakes up (because the home page immediately calls ProductAPI). The other databases (Auth, Coupon, ShoppingCart) remain in Azure SQL auto-pause state until users explicitly navigate to features that require them, causing cold-start delays.

**User Requirements:**
- E-commerce MVP for portfolio
- Min replicas = 1 (containers always running)
- Initial load delay acceptable, but after Web MVC starts, all databases should be warmed up
- Web MVC is the entry point - when it starts, all downstream API databases should be ready

**Azure SQL Auto-Pause Strategy:**
- Keep Azure SQL auto-pause ENABLED for cost optimization
- Databases will pause after 1 hour of inactivity (saves compute costs)
- Warm-up service handles cold starts on container restarts (deployments, crashes)
- Accept occasional cold starts after prolonged idle periods (portfolio/demo acceptable)
- Trade-off: Cost savings vs. occasional 10-30s delay for first user after 1hr+ idle

## Root Cause Analysis

**Current Behavior:**
- Web MVC has NO startup tasks or hosted services
- All API calls are lazy (on-demand when user navigates)
- HomeController.Index() calls ProductAPI on first page load → Product DB wakes up
- Auth, Coupon, and ShoppingCart databases remain cold until user explicitly navigates to those features
- All APIs have lightweight `/health` endpoints that DON'T trigger database operations

**Why Only Product DB Wakes Up:**
The default route (`{controller=Home}/{action=Index}`) immediately calls `_productService.GetAllProductsAsync()` which triggers ProductAPI database access.

**Why Other DBs Stay Cold:**
- **AuthAPI:** Only accessed when user clicks Login/Register
- **CouponAPI:** Only accessed when user navigates to admin panel or applies coupon
- **ShoppingCartAPI:** Only accessed when user clicks cart icon or adds item

## Recommended Solution

**Two-Component Approach:**

### Component 1: DatabaseWarmUpHostedService (Web MVC - REQUIRED)
A background hosted service that runs on Web MVC startup and proactively calls each microservice's health endpoint to trigger database initialization.

**Key Features:**
- Runs asynchronously (non-blocking) on application startup
- Calls all 4 API health endpoints in parallel
- Implements retry logic with configurable attempts
- Comprehensive logging for monitoring
- Fully configurable per environment
- Graceful failure handling (continues if one service fails)

### Component 2: Database-Aware Health Checks (APIs - REQUIRED)
Enhance existing lightweight health endpoints to perform minimal database operations (e.g., `CanConnectAsync()`), ensuring health checks actually wake Azure SQL databases.

**Without this:** Health endpoints return 200 OK without touching the database, so calling them won't wake Azure SQL.
**With this:** Health endpoints open database connections, triggering Azure SQL wake-up.

---

## Implementation Details

### 1. New Files to Create

#### 1.1 Configuration Model
**File:** `E-commerce.Web\Models\WarmUpConfiguration.cs`

```csharp
namespace E_commerce.Web.Models
{
    public class WarmUpConfiguration
    {
        public bool Enabled { get; set; } = true;
        public List<ServiceWarmUpConfig> Services { get; set; } = new();
    }

    public class ServiceWarmUpConfig
    {
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string HealthEndpoint { get; set; } = "/health";
        public int TimeoutMs { get; set; } = 30000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 2000;
    }
}
```

#### 1.2 Hosted Service
**File:** `E-commerce.Web\Services\DatabaseWarmUpHostedService.cs`

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

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
                var successCount = warmUpTasks.Count(t => t.Result);
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
```

### 2. Files to Modify

#### 2.1 Web MVC Configuration
**File:** `E-commerce.Web\appsettings.json`

**Add new section:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "E_commerce.Web.Services.DatabaseWarmUpHostedService": "Information"
    }
  },
  "DatabaseWarmUp": {
    "Enabled": true,
    "Services": [
      {
        "Name": "ProductAPI",
        "BaseUrl": "https://localhost:7000",
        "HealthEndpoint": "/health",
        "TimeoutMs": 30000,
        "MaxRetries": 3,
        "RetryDelayMs": 2000
      },
      {
        "Name": "CouponAPI",
        "BaseUrl": "https://localhost:7001",
        "HealthEndpoint": "/health",
        "TimeoutMs": 30000,
        "MaxRetries": 3,
        "RetryDelayMs": 2000
      },
      {
        "Name": "AuthAPI",
        "BaseUrl": "https://localhost:7002",
        "HealthEndpoint": "/health",
        "TimeoutMs": 30000,
        "MaxRetries": 3,
        "RetryDelayMs": 2000
      },
      {
        "Name": "ShoppingCartAPI",
        "BaseUrl": "https://localhost:7003",
        "HealthEndpoint": "/health",
        "TimeoutMs": 30000,
        "MaxRetries": 3,
        "RetryDelayMs": 2000
      }
    ]
  }
}
```

#### 2.2 Web MVC Program.cs
**File:** `E-commerce.Web\Program.cs`

**Add using statements:**
```csharp
using E_commerce.Web.Services;
using E_commerce.Web.Models;
```

**Add after existing service registrations (before `var app = builder.Build();`):**
```csharp
// Configure database warm-up settings
builder.Services.Configure<WarmUpConfiguration>(
    builder.Configuration.GetSection("DatabaseWarmUp"));

// Register database warm-up hosted service
builder.Services.AddHostedService<DatabaseWarmUpHostedService>();
```

#### 2.3 API Health Endpoints (REQUIRED)
**Files:**
- `E-commerce.Services.ProductAPI\Program.cs`
- `E-commerce.Services.CouponAPI\Program.cs`
- `E-commerce.Services.AuthAPI\Program.cs`
- `E-commerce.Services.ShoppingCartAPI\Program.cs`

**Replace current lightweight health check:**
```csharp
app.MapGet("/health", () => Results.Ok(new {
    status = "healthy",
    service = "ProductAPI",
    timestamp = DateTime.UtcNow
}));
```

**With database-aware version:**
```csharp
app.MapGet("/health", async (ApplicationDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();

        if (!canConnect)
        {
            return Results.Json(
                new { status = "unhealthy", service = "ProductAPI", timestamp = DateTime.UtcNow, database = "disconnected" },
                statusCode: 503);
        }

        return Results.Ok(new {
            status = "healthy",
            service = "ProductAPI",
            timestamp = DateTime.UtcNow,
            database = "connected"
        });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "ProductAPI", timestamp = DateTime.UtcNow, error = ex.Message },
            statusCode: 503);
    }
});
```

**Apply same pattern to all 4 APIs, changing the service name accordingly.**

---

## Configuration for Different Environments

### Development (appsettings.Development.json)
```json
{
  "DatabaseWarmUp": {
    "Enabled": true,
    "Services": [
      {
        "Name": "ProductAPI",
        "BaseUrl": "https://localhost:7000",
        "TimeoutMs": 30000,
        "MaxRetries": 3
      },
      // ... other services
    ]
  }
}
```

### Production (Azure Container Apps)
```json
{
  "DatabaseWarmUp": {
    "Enabled": true,
    "Services": [
      {
        "Name": "ProductAPI",
        "BaseUrl": "https://productapi.azurecontainerapps.io",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      },
      // ... other services with production URLs
    ]
  }
}
```

**Production Adjustments:**
- Increase timeouts to 60s (Azure SQL cold start can take longer)
- Increase max retries to 5 (network reliability)
- Use internal Container Apps URLs when possible

---

## Implementation Steps

### Single Implementation Phase (4-6 hours)

1. **Create Configuration Model**
   - Create `E-commerce.Web\Models\WarmUpConfiguration.cs`
   - Add both configuration classes

2. **Create Hosted Service**
   - Create `E-commerce.Web\Services\` directory (if doesn't exist)
   - Create `E-commerce.Web\Services\DatabaseWarmUpHostedService.cs`
   - Implement full service with retry logic

3. **Update Web MVC Configuration**
   - Edit `E-commerce.Web\appsettings.json`
   - Add `DatabaseWarmUp` section with all 4 services

4. **Register Services in Web MVC**
   - Edit `E-commerce.Web\Program.cs`
   - Add using statements
   - Add configuration binding
   - Register hosted service

5. **Enhance All API Health Endpoints**
   - Edit `E-commerce.Services.ProductAPI\Program.cs`
   - Edit `E-commerce.Services.CouponAPI\Program.cs`
   - Edit `E-commerce.Services.AuthAPI\Program.cs`
   - Edit `E-commerce.Services.ShoppingCartAPI\Program.cs`
   - Replace lightweight health checks with database-aware version

6. **Test Locally**
   - Start all 4 API services
   - Start Web MVC
   - Monitor logs for warm-up messages
   - Verify all services report "ready"
   - Test individual health endpoints

**Expected Log Output:**
```
info: Starting database warm-up for 4 services
info: Warming up ProductAPI at https://localhost:7000/health
info: Warming up CouponAPI at https://localhost:7001/health
info: Warming up AuthAPI at https://localhost:7002/health
info: Warming up ShoppingCartAPI at https://localhost:7003/health
info: ProductAPI is ready (attempt 1/3, 4523ms)
info: CouponAPI is ready (attempt 1/3, 5234ms)
info: AuthAPI is ready (attempt 1/3, 6123ms)
info: ShoppingCartAPI is ready (attempt 1/3, 4856ms)
info: Database warm-up completed: 4/4 services ready in 6234ms
```

---

## Testing Strategy

### Local Testing

1. **Full Warm-Up Test**
   - Ensure databases can be paused (Azure SQL auto-pause or manual pause)
   - Start all 4 API services
   - Start Web MVC
   - Monitor logs for warm-up completion
   - Verify timing (30-60s for cold databases)

2. **Failure Scenario Test**
   - Stop one API service (e.g., AuthAPI)
   - Start Web MVC
   - Verify retry logic works
   - Verify application still starts successfully
   - Check logs show retries and eventual failure

3. **Configuration Tests**
   - Test with `Enabled: false` - verify no warm-up
   - Test with reduced timeouts - verify timeout handling
   - Test with increased retries - verify retry logic

### Production Validation

1. **Azure Application Insights Query**
```kusto
traces
| where message contains "Database warm-up"
| order by timestamp desc
```

2. **Container Logs**
```bash
az containerapp logs show \
  --name webmvc \
  --resource-group ecommerce-rg \
  --follow
```

3. **Database Metrics**
   - Monitor Azure SQL "Active Connections" metric
   - Verify connections spike during Web MVC startup

---

## Key Benefits

1. **User Experience:** No cold-start delays after deployments/container restarts
2. **Cost Optimization:** Azure SQL auto-pause enabled (databases pause after 1hr idle, saving compute costs)
3. **Reliability:** Retry logic handles transient failures
4. **Observability:** Comprehensive logging for troubleshooting
5. **Flexibility:** Configurable per environment (enable/disable, adjust timeouts)
6. **Non-Blocking:** Runs asynchronously without delaying app startup
7. **Zero Additional Infrastructure:** No new Azure resources required
8. **Graceful Degradation:** Application starts even if some services fail to warm up

---

## Success Criteria

### Development
- All 4 services report "ready" in logs
- Total warm-up time < 60s with cold databases
- Application starts successfully even if 1 service fails

### Production
- Warm-up success rate > 95%
- Average warm-up time < 30s
- First user request < 500ms response time
- No cold-start delays for typical user flows

---

## Dependencies

**No New NuGet Packages Required** - Uses existing ASP.NET Core libraries:
- `Microsoft.Extensions.Hosting` (IHostedService)
- `Microsoft.Extensions.Http` (IHttpClientFactory)
- `Microsoft.Extensions.Options` (IOptions<T>)
- `Microsoft.Extensions.Logging` (ILogger<T>)

**Internal Dependencies:**
- Web MVC must have access to API endpoints
- APIs must expose `/health` endpoints (already exist)
- Configuration structure must match binding models

---

## Rollback Plan

If issues arise:

1. **Disable warm-up via configuration:**
   ```json
   { "DatabaseWarmUp": { "Enabled": false } }
   ```

2. **Remove service registration:**
   ```csharp
   // Comment out in Program.cs:
   // builder.Services.AddHostedService<DatabaseWarmUpHostedService>();
   ```

3. **Revert health endpoints** to lightweight version if database-aware version causes issues

**No database schema changes** - purely application-level implementation.

---

## Critical Files Summary

### Files to Create
1. `E-commerce.Web\Models\WarmUpConfiguration.cs` - Configuration classes
2. `E-commerce.Web\Services\DatabaseWarmUpHostedService.cs` - Hosted service implementation

### Files to Modify (All Required)
3. `E-commerce.Web\Program.cs` - Register service and configuration
4. `E-commerce.Web\appsettings.json` - Add DatabaseWarmUp section
5. `E-commerce.Services.ProductAPI\Program.cs` - Database-aware health check
6. `E-commerce.Services.CouponAPI\Program.cs` - Database-aware health check
7. `E-commerce.Services.AuthAPI\Program.cs` - Database-aware health check
8. `E-commerce.Services.ShoppingCartAPI\Program.cs` - Database-aware health check

---

## Estimated Effort

- **Complete Implementation:** 4-6 hours
- **Testing and Validation:** 2-3 hours
- **Production Deployment:** 1-2 hours
- **Total:** 8-10 hours

**Priority:** HIGH - Directly addresses cold-start issue with minimal risk and no infrastructure changes.

---

## Azure SQL Auto-Pause Behavior

### Understanding the Lifecycle

**Container Apps (Min Replicas = 1):**
- Web MVC and API containers are **always running** (never scale to 0)
- Container restart scenarios: deployments, crashes, scale-up after manual scale-down

**Azure SQL Databases (Auto-Pause Enabled):**
- Databases **pause independently** after 1 hour of no queries
- Paused databases consume only storage costs (no compute charges)
- First query after pause triggers wake-up (10-30 seconds delay)
- Auto-pause is independent of container lifecycle

### When Warm-Up Service Triggers

The DatabaseWarmUpHostedService runs when:
1. **Initial deployment** - First time Web MVC container starts
2. **Redeployment** - New version deployed via CI/CD
3. **Container restart** - Crash recovery, manual restart
4. **Scale events** - If manually scaled down then back up

**It does NOT run:**
- During normal operation (once container is running)
- When databases auto-pause due to inactivity (1hr+ no queries)

### Expected User Experience

**Scenario 1: Right After Deployment**
- Warm-up service runs on container startup
- All 4 databases warmed up within 30-60s
- Users experience fast response times
- **Result:** Excellent UX

**Scenario 2: Active Usage (< 1hr between requests)**
- Databases remain active (no auto-pause)
- All features respond quickly
- **Result:** Excellent UX

**Scenario 3: After 1+ Hour Idle**
- Containers still running (min replicas = 1)
- Databases auto-paused to save costs
- First user request to each feature triggers cold start:
  - Home page (Product DB) → 10-30s delay
  - Login (Auth DB) → 10-30s delay
  - Cart (ShoppingCart DB) → 10-30s delay
  - Coupon features (Coupon DB) → 10-30s delay
- Subsequent requests fast
- **Result:** Acceptable for portfolio/demo (cost savings prioritized)

### Cost vs. Performance Trade-off

**Current Strategy (Recommended for Portfolio):**
- Min Replicas = 1 (always running)
- Auto-Pause = Enabled (pause after 1hr idle)
- Warm-Up Service = Enabled (wake on container restart)

**Monthly Cost Estimate:**
- Container Apps: ~$10-20/month (always running)
- Azure SQL (4 databases): ~$5-10/month (mostly paused for low-traffic)
- **Total: ~$15-30/month**

**Alternative (Premium Performance):**
- Min Replicas = 1
- Auto-Pause = Disabled
- Warm-Up Service = Enabled

**Monthly Cost Estimate:**
- Container Apps: ~$10-20/month
- Azure SQL (4 databases): ~$60-120/month (always running)
- **Total: ~$70-140/month**

**Recommendation:** Stick with auto-pause enabled for portfolio/MVP. Demonstrates cost-awareness and cloud optimization skills.

### Portfolio Talking Points

When showcasing this project:
> "Implemented a proactive database warm-up service to eliminate cold starts on deployments while maintaining Azure SQL auto-pause for cost optimization. This reduces monthly Azure costs by 60-70% compared to always-on databases, with acceptable trade-off of occasional cold starts after extended idle periods."

### Future Enhancements (If Needed)

If cold starts after idle become problematic:

1. **Keep-Alive Background Service** (adds ~$5-10/month)
   - Run scheduled queries every 45 minutes to prevent auto-pause
   - Trade-off: Higher costs vs. guaranteed performance

2. **Disable Auto-Pause Selectively**
   - Keep Auth database always warm (critical path)
   - Let others auto-pause (less critical)

3. **Traffic-Based Auto-Wake**
   - Use Azure Logic App to detect traffic patterns
   - Pre-warm databases before expected peak times
