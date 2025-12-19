# Debugging Database Warm-Up Issue on Azure - Root Cause Analysis

## Problem Statement
- Web MVC deployed to Azure Container Apps
- Only ProductAPI database wakes up automatically on startup
- AuthAPI, CouponAPI, ShoppingCartAPI databases remain cold
- When user manually logs in, AuthAPI database wakes up (proving auth health endpoint works)

## Key Observation
The warm-up service IS working (otherwise Product DB wouldn't wake), but only for ProductAPI. This means:
- ✅ Hosted service IS running on startup
- ✅ Configuration IS loaded
- ✅ Health endpoints ARE callable
- ❌ But only 1 out of 4 databases wakes up

---

## Root Cause Analysis - Multiple Possible Causes

### SCOPE 1: Configuration Loading in Production (MOST LIKELY)

**Problem:** The warm-up configuration might not be loaded in Azure production environment.

#### Check 1a: Check Active Environment
When running on Azure Container Apps, the ASPNETCORE_ENVIRONMENT is usually set to "Production" by default.

**Verification:**
```bash
# In Azure Container Apps logs, look for:
info: Start database warm-up for {Count} services
```

If you DON'T see this message, the service didn't even start the warm-up.

**Why this matters:**
- `appsettings.json` is loaded first (contains DatabaseWarmUp config)
- `appsettings.{Environment}.json` is loaded second (merges/overrides)
- If no `appsettings.Production.json` exists → Production uses only base config ✅
- If `appsettings.Production.json` exists but DOESN'T have DatabaseWarmUp → warm-up disabled ❌

**Current files:**
- ✅ `appsettings.json` - HAS DatabaseWarmUp section
- ✅ `appsettings.Development.json` - Might be interfering locally, but not in Azure

**Solution:** Verify you have the DatabaseWarmUp config in the right place.

---

### SCOPE 2: Service URLs in Azure (VERY LIKELY)

**Problem:** The warm-up URLs might be using localhost instead of Azure internal URLs.

#### Check 2a: URLs in appsettings.json
```json
"DatabaseWarmUp": {
  "Services": [
    {
      "Name": "ProductAPI",
      "BaseUrl": "https://localhost:7000",  // ← THIS IS THE PROBLEM!
      ...
    }
  ]
}
```

**Why this is the issue:**
- Localhost doesn't exist inside Azure Container Apps containers
- Container A (Web) cannot reach Container B (ProductAPI) using "localhost"
- Somehow ProductAPI is still being warmed... HOW?

**The Plot Twist:**
ProductAPI wakes because of the HOME PAGE (not warm-up service):
1. Web MVC starts
2. Warm-up tries to call `https://localhost:7000/health` → FAILS (no localhost in Azure)
3. But it retries with longer timeouts...
4. During first user request, home page calls `_productService.GetAllProductsAsync()`
5. This makes a VALID HTTP request using the proper URL from ServiceUrls
6. ProductAPI finally wakes up!

**The Real Issue:**
The warm-up service is calling localhost URLs which don't resolve in Azure Container Apps. The requests fail silently after timeouts.

---

### SCOPE 3: HTTP Client Certificate Validation (POSSIBLE)

**Problem:** HTTPS requests to self-signed certificates might fail in Azure.

#### Check 3a: Certificate Issues
Looking at your code:
```csharp
var response = await client.GetAsync(healthUrl, cancellationToken);
```

If the container's certificate validation rejects the API's certificate, this would throw `HttpRequestException` and get caught on line 122-131.

This would explain why some APIs work (after user manually triggers them) but not during warm-up.

---

### SCOPE 4: API Startup Order (LESS LIKELY)

**Problem:** APIs might not be fully started when warm-up runs.

#### Check 4a: Timing Issue
Web MVC starts first (min replicas = 1), warm-up runs immediately. Other APIs might still be initializing.

The hosted service has retry logic (3 attempts, exponential backoff), which should handle this.

**Less likely because:**
- RetryDelayMs = 2000, so retries at 2s, 4s, 6s
- TimeoutMs = 45000 (45 seconds)
- Total wait time per service: ~15 seconds (best case)
- If all 4 APIs are running, they should all respond within this window

---

## Debugging Steps (In Order)

### Step 1: Enable Debug Logging
Check Application Insights or Container Logs for these messages:

**These SHOULD appear:**
```
info: Starting database warm-up for 4 services
info: Warming up ProductAPI at https://localhost:7000/health
info: Warming up CouponAPI at https://localhost:7001/health
info: Warming up AuthAPI at https://localhost:7002/health
info: Warming up ShoppingCartAPI at https://localhost:7003/health
```

**Then either:**
```
info: ProductAPI is ready (attempt X/3, XXXms)
```

OR:

```
warn: ProductAPI timed out after 45000ms on attempt 1/3
warn: ProductAPI timed out after 45000ms on attempt 2/3
warn: ProductAPI timed out after 45000ms on attempt 3/3
err: ProductAPI failed to warm up after 3 attempts
```

**If you see NO warm-up messages at all** → Hosted service not running (config issue)

**If you see TIMEOUT messages for 3 of the 4 APIs** → URL issue (using localhost)

---

### Step 2: Verify Container App Internal Networking

In Azure Container Apps, use the **internal FQDN** not localhost.

**Pattern:** `{service-name}.internal.{environment-id}.{region}.azurecontainerapps.io`

**Example:**
```
ProductAPI:     https://productapi.internal.azurecontainerapps.io
AuthAPI:        https://authapi.internal.azurecontainerapps.io
CouponAPI:      https://couponapi.internal.azurecontainerapps.io
ShoppingCartAPI: https://shoppingcart.internal.azurecontainerapps.io
```

Or if using Container App names directly:
```
ProductAPI:     https://productapi:7000
AuthAPI:        https://authapi:7002
```

**NOTE:** These URLs are ONLY valid FROM WITHIN the Container Apps environment, not from outside.

---

### Step 3: Certificate Validation Issue

If the internal URLs use HTTPS with self-signed certificates, add this to Program.cs:

```csharp
#if DEBUG
    var httpClientHandler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            // Only in development - skip cert validation for self-signed
            return true;
        }
    };
    builder.Services.AddHttpClient()
        .ConfigurePrimaryHttpMessageHandler(() => httpClientHandler);
#endif
```

For production Azure, you might need:
```csharp
var httpClientHandler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
    {
        // Accept self-signed certs (if used internally)
        if (message?.RequestUri?.Host?.Contains(".internal") == true)
            return true;
        return errors == System.Net.Security.SslPolicyErrors.None;
    }
};
```

---

## The Most Likely Culprit: LOCALHOST URLs IN PRODUCTION

### Why Only ProductAPI Wakes Up

**Timeline:**
```
T=0s  Web MVC Container starts
T=2s  Warm-up service starts
      ├─ Tries ProductAPI: https://localhost:7000/health
      │  └─ FAILS (localhost doesn't exist in Azure)
      ├─ Tries CouponAPI: https://localhost:7001/health
      │  └─ FAILS
      ├─ Tries AuthAPI: https://localhost:7002/health
      │  └─ FAILS
      └─ Tries ShoppingCartAPI: https://localhost:7003/health
         └─ FAILS

T=30s Warm-up times out on all retries
      All databases still cold

T=35s User navigates to home page
      ├─ HomeController calls _productService.GetAllProductsAsync()
      ├─ This uses the CORRECT URL from ServiceUrls (not localhost!)
      ├─ Makes HTTP call to ProductAPI
      ├─ ProductAPI database WAKES UP ✅
      └─ User sees products

T=40s User clicks Login button
      ├─ AuthController calls _authService.Login()
      ├─ Makes HTTP call to AuthAPI
      ├─ AuthAPI database WAKES UP ✅
      └─ User logs in
```

**This explains everything perfectly!**

---

## Solution: Create appsettings.Production.json

Create this file: `E-commerce.Web/appsettings.Production.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "E_commerce.Web.Services.DatabaseWarmUpHostedService": "Information"
    }
  },
  "ServiceUrls": {
    "CouponAPI": "https://couponapi.internal.{YOUR-ENV}.azurecontainerapps.io",
    "AuthAPI": "https://authapi.internal.{YOUR-ENV}.azurecontainerapps.io",
    "ProductAPI": "https://productapi.internal.{YOUR-ENV}.azurecontainerapps.io",
    "ShoppingCartAPI": "https://shoppingcart.internal.{YOUR-ENV}.azurecontainerapps.io"
  },
  "DatabaseWarmUp": {
    "Enabled": true,
    "Services": [
      {
        "Name": "ProductAPI",
        "BaseUrl": "https://productapi.internal.{YOUR-ENV}.azurecontainerapps.io",
        "HealthEndpoint": "/health",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      },
      {
        "Name": "CouponAPI",
        "BaseUrl": "https://couponapi.internal.{YOUR-ENV}.azurecontainerapps.io",
        "HealthEndpoint": "/health",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      },
      {
        "Name": "AuthAPI",
        "BaseUrl": "https://authapi.internal.{YOUR-ENV}.azurecontainerapps.io",
        "HealthEndpoint": "/health",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      },
      {
        "Name": "ShoppingCartAPI",
        "BaseUrl": "https://shoppingcart.internal.{YOUR-ENV}.azurecontainerapps.io",
        "HealthEndpoint": "/health",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      }
    ]
  }
}
```

**Replace `{YOUR-ENV}` with your actual Container Apps environment identifier.**

---

## How to Find Your Internal URLs

### Option 1: Azure Portal
1. Go to Container Apps environment
2. View Container Apps resource
3. Check the "Ingress" settings
4. Note the FQDN pattern

### Option 2: Azure CLI
```bash
az containerapp list --resource-group <your-rg> --output table
```

### Option 3: Application Insights Query
When apps call each other internally, the logs show the actual URLs used.

---

## Verification After Fix

After updating appsettings.Production.json and redeploying:

1. **Check logs in Application Insights:**
```kusto
traces
| where message contains "Starting database warm-up"
| order by timestamp desc
```

Should see:
```
info: Starting database warm-up for 4 services
info: Warming up ProductAPI at https://productapi.internal.{env}.azurecontainerapps.io/health
info: Warming up CouponAPI at https://couponapi.internal.{env}.azurecontainerapps.io/health
info: Warming up AuthAPI at https://authapi.internal.{env}.azurecontainerapps.io/health
info: Warming up ShoppingCartAPI at https://shoppingcart.internal.{env}.azurecontainerapps.io/health
info: ProductAPI is ready (attempt 1/5, XXXms)
info: CouponAPI is ready (attempt 1/5, XXXms)
info: AuthAPI is ready (attempt 1/5, XXXms)
info: ShoppingCartAPI is ready (attempt 1/5, XXXms)
info: Database warm-up completed: 4/4 services ready in XXXms
```

2. **Verify timing:**
Should take 30-60 seconds total (not just for ProductAPI)

3. **Test without manual trigger:**
Navigate to login page without first visiting home page. Auth DB should already be warm.

---

## Secondary Issues to Address

### Issue 1: Certificate Validation
If you see `HttpRequestException` in logs for certificate errors:

Add certificate bypass (for internal Azure requests only):
```csharp
if (message?.RequestUri?.Host?.Contains(".internal") == true)
    return true;
```

### Issue 2: Timeout Values
Current production values in solution above:
- `TimeoutMs`: 60000 (60 seconds - increased from 45)
- `MaxRetries`: 5 (increased from 3 for cloud reliability)
- `RetryDelayMs`: 3000 (increased from 2000)

These are more appropriate for Azure SQL cold starts.

### Issue 3: Environment Variable Override
Optionally, set `ASPNETCORE_ENVIRONMENT` explicitly in Azure:
```bash
az containerapp update \
  --name webmvc \
  --resource-group <your-rg> \
  --set-env-vars ASPNETCORE_ENVIRONMENT=Production
```

---

## Summary of Root Causes (Ranked by Likelihood)

| Rank | Cause | Evidence | Fix |
|------|-------|----------|-----|
| 1 | **Localhost URLs in Production** | Only Product wakes (via home page call) | Use Azure internal URLs |
| 2 | **Missing appsettings.Production.json** | Config not loaded | Create Production config file |
| 3 | **Certificate Validation** | HTTPS to self-signed fails | Add cert bypass for internal |
| 4 | **API Startup Order** | APIs not ready | Already handled by retry logic |
| 5 | **Hosted Service Not Running** | No warm-up messages at all | Check Enabled flag in config |

---

## Testing Checklist

- [ ] Check Application Insights for warm-up startup messages
- [ ] Verify all 4 services show "is ready" messages
- [ ] Note the timing (should be 30-60s, not instant)
- [ ] Test logging in WITHOUT first visiting home page
- [ ] Test cart functionality WITHOUT first visiting home page
- [ ] Check Azure Container Apps environment FQDN format
- [ ] Verify internal networking between containers
- [ ] Confirm ASPNETCORE_ENVIRONMENT is set to "Production"

---

## Files to Check/Modify

**Check:**
- [x] Web MVC appsettings.json - Has DatabaseWarmUp config
- [ ] Application Insights logs - Shows warm-up messages
- [ ] Azure Container Apps settings - Environment name
- [ ] Container App internal FQDN - For correct URLs

**Create/Modify:**
- [ ] Create appsettings.Production.json with correct internal URLs
- [ ] Optional: Add certificate validation bypass in Program.cs
- [ ] Optional: Increase timeout/retry values for production

