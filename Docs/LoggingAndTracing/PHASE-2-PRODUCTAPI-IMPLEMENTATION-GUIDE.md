# Phase 2 Implementation Guide: Add Serilog to ProductAPI

> **Goal**: Roll out Serilog structured logging to ProductAPI (first of the remaining 4 services)
>
> **Estimated Time**: 30-45 minutes
> **Prerequisites**: Phase 1 (AuthAPI) completed and tested successfully
> **Reference**: [PHASE-1-IMPLEMENTATION-GUIDE.md](PHASE-1-IMPLEMENTATION-GUIDE.md) for patterns and conventions

---

## 📋 Overview

This guide walks you through adding Serilog to ProductAPI, applying the exact same patterns established in Phase 1 (AuthAPI). ProductAPI is an ideal second service because:

- ✅ **Similar structure to AuthAPI** - Standard controller with CRUD endpoints
- ✅ **No external message publishing** - Simpler than ShoppingCartAPI
- ✅ **Clear business operations** - Get all, Get by ID, Create, Update, Delete (easy to log)
- ✅ **Mid-complexity endpoints** - More to log than AuthAPI's 3 endpoints (5 endpoints total)

---

## 🎯 Current State Analysis

### ProductAPI Structure

**Controller:** `E-commerce.Services.ProductAPI/Controllers/ProductAPIController.cs`

| Endpoint | Method | Auth | Purpose | Current Logging |
|----------|--------|------|---------|-----------------|
| `/api/product` | GET | No | Fetch all products | ❌ None |
| `/api/product/{id}` | GET | No | Fetch product by ID | ❌ None |
| `/api/product` | POST | ADMIN | Create new product | ❌ None |
| `/api/product` | PUT | ADMIN | Update product | ❌ None |
| `/api/product/{id}` | DELETE | ADMIN | Delete product | ❌ None |

**Additional Components:**

| Component | Current State |
|-----------|---------------|
| **Program.cs** | ❌ No Serilog, silent health check, silent migrations |
| **appsettings.json** | ❌ Old "Logging" section (default framework logging) |
| **Error Handling** | ❌ Catch-all exceptions, no logging |
| **Database Migrations** | ❌ Silent ApplyMigration() method |

---

## 🔧 Implementation Steps

### Step 1: Add NuGet Packages

**Action:** Install same 6 Serilog packages in ProductAPI

**Open NuGet Package Manager:**
1. Right-click `E-commerce.Services.ProductAPI` → Manage NuGet Packages
2. Click "Browse" tab
3. Search and install (one at a time):
   - ✅ `Serilog.AspNetCore` (8.0.0 or latest)
   - ✅ `Serilog.Sinks.Console` (5.0.1 or latest)
   - ✅ `Serilog.Sinks.File` (5.0.0 or latest)
   - ✅ `Serilog.Sinks.Seq` (7.0.1 or latest)
   - ✅ `Serilog.Enrichers.Environment` (2.3.0 or latest)
   - ✅ `Serilog.Enrichers.Thread` (3.2.0 or latest)

**Verification:**
- Build succeeds (Ctrl+Shift+B)
- No red squiggly lines in Program.cs or appsettings.json

---

### Step 2: Update appsettings.json

**File:** `E-commerce.Services.ProductAPI/appsettings.json`

**Action:**
1. **DELETE** the existing `"Logging"` section (lines 2-7)
2. **Add** the `"Serilog"` section (exact same as AuthAPI, with service name change)

**Replace entire file with:**

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
          "path": "logs/productapi-.log",
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
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "ApiSettings": {
    "Secret": "",
    "Issuer": "e-commerce-auth-api",
    "Audience": "e-commerce-client"
  }
}
```

**Key Changes from Original:**
- Line 23: Changed log file path from `"logs/authapi-.log"` to `"logs/productapi-.log"`
- ✅ Everything else identical to AuthAPI configuration

**Verification:**
- Save file (Ctrl+S)
- No red squiggly lines in JSON
- Format document (Ctrl+K, Ctrl+D) to verify valid JSON

---

### Step 3: Configure Serilog in Program.cs

**File:** `E-commerce.Services.ProductAPI/Program.cs`

#### 3.1 Add Using Statement

**At the top of the file** (after existing usings), add:

```csharp
using Serilog;
```

**Your using block should look like:**
```csharp
using AutoMapper;
using E_commerce.Services.ProductAPI;
using E_commerce.Services.ProductAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.OpenApi.Models;
using E_commerce.Services.ProductAPI.Extensions;
using Serilog;  // ← ADD THIS
```

#### 3.2 Add Serilog Configuration

**Immediately after** `var builder = WebApplication.CreateBuilder(args);` (line 12), insert:

```csharp
// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Service", "ProductAPI")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .CreateLogger();

builder.Host.UseSerilog();
```

**Why This Order Matters:**
- Serilog must be configured BEFORE other services (before AddDbContext, AddControllers, etc.)
- This ensures startup logs are captured

#### 3.3 Add Logging to Health Endpoint

**Find the health endpoint** (around line 113-118), specifically the exception handler:

**BEFORE:**
```csharp
catch (Exception ex)
{
    return Results.Json(
        new { status = "unhealthy", service = "ProductAPI", timestamp = DateTime.UtcNow, error = ex.Message },
        statusCode: 503);
}
```

**AFTER:**
```csharp
catch (Exception ex)
{
    Log.Warning(ex, "Health check failed - database connectivity issue");
    return Results.Json(
        new { status = "unhealthy", service = "ProductAPI", timestamp = DateTime.UtcNow, error = ex.Message },
        statusCode: 503);
}
```

#### 3.4 Add Try-Catch-Finally Around app.Run()

**Find** `app.Run();` (currently line 126)

**BEFORE:**
```csharp
if (!app.Environment.IsProduction())
{
	ApplyMigration();
}

app.Run();
```

**AFTER:**
```csharp
if (!app.Environment.IsProduction())
{
	ApplyMigration();
}

try
{
	Log.Information("Starting ProductAPI service");
	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "ProductAPI terminated unexpectedly");
	throw;
}
finally
{
	Log.CloseAndFlush();
}
```

#### 3.5 Enhance ApplyMigration() Method

**Find** the `ApplyMigration()` method (around lines 128-143)

**BEFORE:**
```csharp
void ApplyMigration()
{
	// I want to get the ApplicationDbContext service here and check if there are any pending migration.
	// If there are any pending migration, I want to apply them.

	// Get all the services from the service container
	using (var scope = app.Services.CreateScope())
	{
		var _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		if (_db.Database.GetPendingMigrations().Count() > 0)
		{
			_db.Database.Migrate();
		}
	}
}
```

**AFTER:**
```csharp
void ApplyMigration()
{
	// I want to get the ApplicationDbContext service here and check if there are any pending migration.
	// If there are any pending migration, I want to apply them.

	// Get all the services from the service container
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

**Verification:**
- Save file (Ctrl+S)
- Build succeeds (Ctrl+Shift+B)
- No red squiggly lines

---

### Step 4: Add ILogger to ProductAPIController

**File:** `E-commerce.Services.ProductAPI/Controllers/ProductAPIController.cs`

#### 4.1 Add ILogger Field

**Find the private fields** (around line 16-20):

**BEFORE:**
```csharp
private readonly ApplicationDbContext _db;
private ResponseDto _response;
private IMapper _mapper;
```

**AFTER:**
```csharp
private readonly ApplicationDbContext _db;
private readonly ILogger<ProductAPIController> _logger;
private ResponseDto _response;
private IMapper _mapper;
```

#### 4.2 Update Constructor

**BEFORE:**
```csharp
public ProductAPIController(ApplicationDbContext db, IMapper mapper)
{
    _db = db;
    _response = new ResponseDto();
    _mapper = mapper;
}
```

**AFTER:**
```csharp
public ProductAPIController(ApplicationDbContext db, IMapper mapper, ILogger<ProductAPIController> logger)
{
    _db = db;
    _logger = logger;
    _response = new ResponseDto();
    _mapper = mapper;
}
```

#### 4.3 Add Logging to Get() Endpoint (All Products)

**BEFORE:**
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

**AFTER:**
```csharp
[HttpGet]
public ResponseDto Get()
{
    _logger.LogInformation("Fetching all products");

    try
    {
        IEnumerable<Product> objList = _db.Products.ToList();
        _response.Result = _mapper.Map<IEnumerable<ProductDto>>(objList);
        _logger.LogInformation("Successfully retrieved {Count} products", objList.Count());
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching all products");
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

#### 4.4 Add Logging to Get(id) Endpoint

**BEFORE:**
```csharp
[HttpGet]
[Route("{id:int}")]
public ResponseDto Get(int id)
{
    try
    {
        Product obj = _db.Products.First(u => u.ProductId == id);
        _response.Result = _mapper.Map<ProductDto>(obj);
    }
    catch (Exception ex)
    {
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

**AFTER:**
```csharp
[HttpGet]
[Route("{id:int}")]
public ResponseDto Get(int id)
{
    _logger.LogInformation("Fetching product with ID {ProductId}", id);

    try
    {
        Product obj = _db.Products.First(u => u.ProductId == id);
        _response.Result = _mapper.Map<ProductDto>(obj);
        _logger.LogInformation("Successfully retrieved product {ProductId}", id);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching product {ProductId}", id);
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

#### 4.5 Add Logging to Post() Endpoint (Create Product)

**BEFORE:**
```csharp
[HttpPost]
[Authorize(Roles = "ADMIN")]
public ResponseDto Post([FromBody] ProductDto ProductDto)
{
    try
    {
        Product obj = _mapper.Map<Product>(ProductDto);
        _db.Products.Add(obj);
        _db.SaveChanges();
        _response.Result = _mapper.Map<ProductDto>(obj);
    }
    catch (Exception ex)
    {
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

**AFTER:**
```csharp
[HttpPost]
[Authorize(Roles = "ADMIN")]
public ResponseDto Post([FromBody] ProductDto ProductDto)
{
    _logger.LogInformation("Creating new product: {ProductName}", ProductDto.Name);

    try
    {
        Product obj = _mapper.Map<Product>(ProductDto);
        _db.Products.Add(obj);
        _db.SaveChanges();
        _response.Result = _mapper.Map<ProductDto>(obj);
        _logger.LogInformation("Product created successfully with ID {ProductId}", obj.ProductId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating product {ProductName}", ProductDto.Name);
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

#### 4.6 Add Logging to Put() Endpoint (Update Product)

**BEFORE:**
```csharp
[HttpPut]
[Authorize(Roles = "ADMIN")]
public ResponseDto Put([FromBody] ProductDto ProductDto)
{
    try
    {
        Product obj = _mapper.Map<Product>(ProductDto);
        _db.Products.Update(obj);
        _db.SaveChanges();
        _response.Result = _mapper.Map<ProductDto>(obj);
    }
    catch (Exception ex)
    {
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

**AFTER:**
```csharp
[HttpPut]
[Authorize(Roles = "ADMIN")]
public ResponseDto Put([FromBody] ProductDto ProductDto)
{
    _logger.LogInformation("Updating product {ProductId}: {ProductName}", ProductDto.ProductId, ProductDto.Name);

    try
    {
        Product obj = _mapper.Map<Product>(ProductDto);
        _db.Products.Update(obj);
        _db.SaveChanges();
        _response.Result = _mapper.Map<ProductDto>(obj);
        _logger.LogInformation("Product {ProductId} updated successfully", ProductDto.ProductId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating product {ProductId}", ProductDto.ProductId);
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

#### 4.7 Add Logging to Delete() Endpoint

**BEFORE:**
```csharp
[HttpDelete]
[Route("{id:int}")]
[Authorize(Roles = "ADMIN")]
public ResponseDto Delete(int id)
{
    try
    {
        Product obj = _db.Products.First(u=>u.ProductId == id);
        _db.Products.Remove(obj);
        _db.SaveChanges();
    }
    catch (Exception ex)
    {
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

**AFTER:**
```csharp
[HttpDelete]
[Route("{id:int}")]
[Authorize(Roles = "ADMIN")]
public ResponseDto Delete(int id)
{
    _logger.LogInformation("Deleting product {ProductId}", id);

    try
    {
        Product obj = _db.Products.First(u=>u.ProductId == id);
        _db.Products.Remove(obj);
        _db.SaveChanges();
        _logger.LogInformation("Product {ProductId} deleted successfully", id);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error deleting product {ProductId}", id);
        _response.IsSuccess = false;
        _response.Message = ex.Message;
    }
    return _response;
}
```

**Verification:**
- Save file (Ctrl+S)
- Build succeeds (Ctrl+Shift+B)
- No red squiggly lines

---

## ✅ Verification Checklist

Before proceeding to Phase 2 testing, verify all changes:

- [ ] All 6 Serilog packages installed in ProductAPI
- [ ] appsettings.json has "Serilog" section (old "Logging" removed)
- [ ] appsettings.json file path changed to `"logs/productapi-.log"`
- [ ] Program.cs imports Serilog (`using Serilog;`)
- [ ] Program.cs has LoggerConfiguration after `var builder = ...`
- [ ] Program.cs has `builder.Host.UseSerilog();`
- [ ] Health endpoint exception handler logs with Log.Warning()
- [ ] ApplyMigration() method has try-catch with comprehensive logging
- [ ] app.Run() wrapped in try-catch-finally with Log.CloseAndFlush()
- [ ] ProductAPIController has ILogger field
- [ ] ProductAPIController constructor injects ILogger
- [ ] All 5 endpoints have logging (Get all, Get by ID, Post, Put, Delete)
- [ ] Each endpoint has 2-3 log calls (attempt, success/failure)
- [ ] Solution builds with 0 errors (Ctrl+Shift+B)
- [ ] No red squiggly lines in any modified files

---

## 🧪 Testing Phase 2

### Step 1: Set ProductAPI as Startup Project

1. Right-click `E-commerce.Services.ProductAPI` in Solution Explorer
2. Select "Set as Startup Project"
3. Project name becomes bold in Solution Explorer

### Step 2: Run ProductAPI (F5)

1. Press F5 or click green Play button
2. Wait for service to start
3. Check **Visual Studio Output window** (View → Output) for log output

**Expected Console Output:**
```
[10:45:23 INF] Starting ProductAPI service
[10:45:24 INF] Microsoft.Hosting.Lifetime | Now listening on: https://localhost:7000
[10:45:24 INF] Microsoft.Hosting.Lifetime | Application started. Press Ctrl+C to shut down.
```

✅ **Checkpoint 1:** Colored console output with timestamps appears

### Step 3: Verify Seq Receives Logs

1. Open browser: **http://localhost:5341** (Seq dashboard)
2. You should see logs from ProductAPI appearing
3. Click on any log entry to expand details
4. Verify log has these properties:
   - `Service`: "ProductAPI"
   - `Environment`: "Development"
   - `MachineName`: (your computer)
   - `ThreadId`: (a number)

✅ **Checkpoint 2:** Logs appear in Seq with structured properties

### Step 4: Test Endpoints via Swagger

1. Open Swagger: **https://localhost:7000/swagger**
2. Expand **GET /api/product** (Get all products)
3. Click "Try it out"
4. Click "Execute"
5. Check response status: Should be **200 OK**

**Check Seq:**
- New log entry should appear: `[INF] Fetching all products`
- Followed by: `[INF] Successfully retrieved {Count} products` with count number

✅ **Checkpoint 3:** GET all products logs appear in Seq

### Step 5: Test Get Product by ID

1. In Swagger, expand **GET /api/product/{id}**
2. Click "Try it out"
3. Enter ID: `1` (default test product)
4. Click "Execute"
5. Check response: **200 OK**

**Check Seq:**
- New logs should appear:
  - `[INF] Fetching product with ID 1`
  - `[INF] Successfully retrieved product 1`

✅ **Checkpoint 4:** GET by ID logs appear in Seq

### Step 6: Test Create Product (Admin Only)

**Note:** You'll need a valid JWT token. For this test, we'll test authorization failure:

1. In Swagger, expand **POST /api/product**
2. Click "Try it out"
3. Enter JSON body:
```json
{
  "productId": 0,
  "name": "Test Product",
  "price": 999.99,
  "description": "Test product"
}
```
4. Click "Execute"
5. Check response: Should be **403 Forbidden** (no auth token)

**Note:** POST will fail due to missing JWT, but we're verifying the logging infrastructure. Full testing of authenticated endpoints requires Phase 3 or integration testing.

### Step 7: Verify File Logs

1. **Stop debugging** (Shift+F5)
2. Navigate to: `E-commerce.Services.ProductAPI\logs\`
3. You should see file: `productapi-2025-12-20.log` (today's date)
4. Open with Notepad
5. You should see structured log entries:
```
2025-12-20 10:45:23.456 +03:00 [INF] E_commerce.Services.ProductAPI.Program | ProductAPI |  | Starting ProductAPI service
2025-12-20 10:45:24.789 +03:00 [INF] E_commerce.Services.ProductAPI.Controllers.ProductAPIController | ProductAPI |  | Fetching all products
```

✅ **Checkpoint 5:** Log file created with rolling format

### Step 8: Test Error Scenario

1. Run ProductAPI again (F5)
2. In Swagger, try to Get product with invalid ID (e.g., 99999)
3. Should return response (either 200 with null or 500 with error)

**Check Seq:**
- Should see **ERROR** level log (red in Seq):
  - `[ERR] Error fetching product 99999`
  - With exception details in the log entry

✅ **Checkpoint 6:** Errors logged at correct level

---

## 📊 Expected Behavior

### When ProductAPI Starts:
1. Console shows startup logs in real-time
2. Seq receives and displays startup events
3. Log file created in `logs/productapi-YYYY-MM-DD.log`
4. Database migrations applied (if needed) with logging

### When Requests Hit Endpoints:
1. **Console:** Immediate colored log output
2. **Seq:** Structured logs with metadata (Service, Environment, ThreadId, CorrelationId placeholder)
3. **File:** Append to rolling daily log file
4. **Response time:** <1ms per log call (Serilog is optimized)

### When Errors Occur:
1. **Console:** Red [ERR] or [WRN] output
2. **Seq:** Exception stack traces visible in log details
3. **File:** Full exception details for debugging

---

## 🐛 Troubleshooting

### No Logs in Seq

**Check:**
1. Is Seq running? `docker ps | grep seq`
2. Open http://localhost:5341 - page loads?
3. Check Visual Studio Output window for errors

**Fix:**
```powershell
docker restart seq
```

### Build Errors

**"The type or namespace name 'Serilog' could not be found"**

**Fix:**
1. Right-click ProductAPI → Manage NuGet Packages
2. Click "Installed" tab
3. Verify Serilog.AspNetCore is there
4. If not, reinstall it
5. Clean solution (Build → Clean Solution)
6. Rebuild

### Red Squiggly Lines in JSON

**Fix:**
- Press Ctrl+K, Ctrl+D in appsettings.json to format
- Check for missing commas between sections

---

## 📝 Summary of Changes

| File | Changes | Lines Changed |
|------|---------|----------------|
| `appsettings.json` | Replace Logging with Serilog config | Delete 6, Add 35 |
| `Program.cs` | Add Serilog setup, logging to health endpoint, migration logging, try-catch-finally | Add ~40 |
| `ProductAPIController.cs` | Add ILogger injection, logging to 5 endpoints | Add ~25 |
| **Total** | **3 files modified** | **~100 lines added** |

**Key Differences from AuthAPI:**
- ✅ 5 endpoints instead of 3
- ✅ Uses `{ProductId}` instead of `{Email}` (product-specific)
- ✅ Uses `{Count}` for collection sizes
- ✅ Uses `{ProductName}` for product name
- ✅ Everything else follows exact same pattern

---

## ✨ What's Next

After Phase 2 is tested and verified:

- **Phase 3** → Roll out to CouponAPI (similar to ProductAPI)
- **Phase 4** → Roll out to ShoppingCartAPI (includes service-to-service calls)
- **Phase 5** → Roll out to EmailAPI (message consumer)
- **Phase 3+** → Add Correlation IDs and OpenTelemetry

---

## 🔗 References

- **Phase 1 Guide:** [PHASE-1-IMPLEMENTATION-GUIDE.md](PHASE-1-IMPLEMENTATION-GUIDE.md)
- **Full Observability Guide:** [OBSERVABILITY-IMPLEMENTATION-GUIDE.md](OBSERVABILITY-IMPLEMENTATION-GUIDE.md)
- **Serilog Documentation:** https://serilog.net/
- **Seq Documentation:** https://docs.datalust.co/docs/getting-started

---

**Status:** Ready for implementation
**Estimated Total Time:** 30-45 minutes (Steps 1-4) + 15-20 minutes (Testing)
**Next Action:** Begin with Step 1 - Install NuGet packages
