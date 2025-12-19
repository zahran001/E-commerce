# Warm-Up Service Debugging - Issue Summary & Solution

## The Problem
- Web MVC deployed to Azure Container Apps
- Database warm-up service runs but only ProductAPI database wakes up
- AuthAPI, CouponAPI, ShoppingCartAPI remain cold until manually accessed

## The Root Cause (99% Certain)

### **You're using LOCALHOST URLs in production**

The appsettings.json file has:
```json
"DatabaseWarmUp": {
  "Services": [
    {
      "Name": "ProductAPI",
      "BaseUrl": "https://localhost:7000",  // ❌ INVALID IN AZURE!
      ...
    }
  ]
}
```

**Why this fails:**
- In Azure Container Apps, containers cannot reach each other using `localhost`
- `localhost` = "this container", not "ProductAPI container"
- Warm-up service tries to call `https://localhost:7000/health` → CONNECTION TIMEOUT
- This happens for all 4 services → All timeout → No databases wake up

**Why only ProductAPI appears to wake up:**
- ProductAPI doesn't actually wake from warm-up service
- It wakes when user visits HOME PAGE
- HomeController calls `_productService.GetAllProductsAsync()`
- This uses the CORRECT ServiceUrl from ServiceUrls config (not localhost)
- ProductAPI finally connects and database wakes up
- User sees this happening and thinks warm-up worked for ProductAPI only

---

## The Solution

### Create `E-commerce.Web/appsettings.Production.json`

This file tells the warm-up service to use Azure internal URLs instead of localhost.

**Step 1:** Find your Container Apps environment ID
- Go to Azure Portal → Container Apps environment
- Look at the FQDN of your Web MVC app
- It looks like: `https://webmvc.{ENVID}.{region}.azurecontainerapps.io`
- Extract the middle part (e.g., `mangofruit`)

**Step 2:** Create the file with your environment ID:

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
    "ProductAPI": "https://productapi.internal.{ENVID}.azurecontainerapps.io",
    "CouponAPI": "https://couponapi.internal.{ENVID}.azurecontainerapps.io",
    "AuthAPI": "https://authapi.internal.{ENVID}.azurecontainerapps.io",
    "ShoppingCartAPI": "https://shoppingcart.internal.{ENVID}.azurecontainerapps.io"
  },
  "DatabaseWarmUp": {
    "Enabled": true,
    "Services": [
      {
        "Name": "ProductAPI",
        "BaseUrl": "https://productapi.internal.{ENVID}.azurecontainerapps.io",
        "HealthEndpoint": "/health",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      },
      {
        "Name": "CouponAPI",
        "BaseUrl": "https://couponapi.internal.{ENVID}.azurecontainerapps.io",
        "HealthEndpoint": "/health",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      },
      {
        "Name": "AuthAPI",
        "BaseUrl": "https://authapi.internal.{ENVID}.azurecontainerapps.io",
        "HealthEndpoint": "/health",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      },
      {
        "Name": "ShoppingCartAPI",
        "BaseUrl": "https://shoppingcart.internal.{ENVID}.azurecontainerapps.io",
        "HealthEndpoint": "/health",
        "TimeoutMs": 60000,
        "MaxRetries": 5,
        "RetryDelayMs": 3000
      }
    ]
  }
}
```

**Step 3:** Replace `{ENVID}` with your actual environment ID

Example: If your Web MVC URL is:
```
https://webmvc.mangofruit.eastus.azurecontainerapps.io
```

Then your file should be:
```json
{
  ...
  "ServiceUrls": {
    "ProductAPI": "https://productapi.internal.mangofruit.eastus.azurecontainerapps.io",
    ...
  },
  "DatabaseWarmUp": {
    ...
    "Services": [
      {
        "Name": "ProductAPI",
        "BaseUrl": "https://productapi.internal.mangofruit.eastus.azurecontainerapps.io",
        ...
      },
      ...
    ]
  }
}
```

**Step 4:** Deploy
- Commit the file
- Push to GitHub
- Trigger Azure deployment (CI/CD pipeline)
- Wait for Web MVC to redeploy

**Step 5:** Verify in Application Insights

Query:
```kusto
traces
| where timestamp > ago(10m)
| where message contains "Database warm-up completed"
| project timestamp, message
```

You should now see:
```
Database warm-up completed: 4/4 services ready in XXXms
```

Instead of:
```
[no warm-up messages or only ProductAPI wakes]
```

---

## Why This Happens

### Application Configuration Merge Order

```
1. appsettings.json (base)
   ↓
2. appsettings.{ASPNETCORE_ENVIRONMENT}.json (overrides/merges)
```

**On your local machine (Development):**
- ASPNETCORE_ENVIRONMENT = "Development"
- Loads: appsettings.json + appsettings.Development.json
- appsettings.Development.json doesn't have DatabaseWarmUp, so base config used ✅

**In Azure Container Apps (Production):**
- ASPNETCORE_ENVIRONMENT = "Production" (default)
- Should load: appsettings.json + appsettings.Production.json
- But appsettings.Production.json didn't exist
- So only appsettings.json was used (with localhost URLs) ❌

**With this fix:**
- appsettings.Production.json now exists
- It has DatabaseWarmUp with correct Azure internal URLs ✅
- Warm-up service uses those URLs ✅
- All 4 databases wake up ✅

---

## Secondary Issues (Optional)

If after creating appsettings.Production.json you STILL see issues:

### Issue 1: SSL Certificate Validation Error

**Symptom:** Application Insights shows:
```
The SSL connection could not be established
```

**Cause:** Self-signed certificates on internal APIs

**Fix:** Add to Program.cs:

```csharp
// Add this BEFORE other service registrations
var httpClientHandler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
    {
        // For internal Azure Container Apps, skip cert validation
        if (message?.RequestUri?.Host?.Contains(".internal") == true)
            return true;

        // For external calls, verify cert
        return errors == System.Net.Security.SslPolicyErrors.None;
    }
};

builder.Services.AddHttpClient()
    .ConfigurePrimaryHttpMessageHandler(() => httpClientHandler);
```

### Issue 2: Container Names Don't Match

**Symptom:** Internal URLs valid but still timeout

**Cause:** Container names in Azure don't match what you used in config

**Fix:** Check actual container names:
```bash
az containerapp list --resource-group <your-rg> -o table
```

Use those exact names in appsettings.Production.json.

### Issue 3: Certificate Common Name Mismatch

**Symptom:** Certificate error mentioning "CN=..."

**Cause:** Certificate issued for different hostname

**Current workaround:** Use the certificate bypass above

**Long-term fix:** Issue proper certificates or use HTTP internally

---

## How to Verify Everything Works

After deploying the fix:

### Test 1: Check Logs
```kusto
traces
| where timestamp > ago(30m)
| where message contains "warm-up"
| order by timestamp desc
```

Expected output:
```
Starting database warm-up for 4 services
Warming up ProductAPI at https://productapi.internal.{env}.azurecontainerapps.io/health
Warming up CouponAPI at https://couponapi.internal.{env}.azurecontainerapps.io/health
Warming up AuthAPI at https://authapi.internal.{env}.azurecontainerapps.io/health
Warming up ShoppingCartAPI at https://shoppingcart.internal.{env}.azurecontainerapps.io/health
ProductAPI is ready (attempt 1/5, 5234ms)
CouponAPI is ready (attempt 1/5, 6123ms)
AuthAPI is ready (attempt 1/5, 6234ms)
ShoppingCartAPI is ready (attempt 1/5, 5856ms)
Database warm-up completed: 4/4 services ready in 6234ms
```

### Test 2: Feature Access Without Manual Trigger
1. Open app in new browser
2. Navigate directly to `/Auth/Login` (without visiting home page first)
3. Page loads quickly ✅ (AuthAPI already warmed)
4. Navigate to cart
5. Cart loads quickly ✅ (ShoppingCartAPI already warmed)

### Test 3: Database Metrics
In Azure SQL Portal:
- Check "Active Connections" metric
- During Web MVC startup, should spike to 4+ connections simultaneously
- All 4 databases showing activity

---

## Files to Manage

### NEW FILE TO CREATE:
- `E-commerce.Web/appsettings.Production.json` ← **CREATE THIS**

### EXISTING FILES (don't change):
- `E-commerce.Web/appsettings.json` - Keep as-is (for dev/defaults)
- `E-commerce.Web/appsettings.Development.json` - Keep as-is
- `E-commerce.Web/Services/DatabaseWarmUpHostedService.cs` - Already correct
- `E-commerce.Web/Program.cs` - Already correct
- All API Program.cs files - Already have database-aware health endpoints

---

## Deployment Checklist

- [ ] Created appsettings.Production.json
- [ ] Replaced `{ENVID}` with actual Container Apps environment ID
- [ ] Container names in config match actual Container App names
- [ ] Committed file to git
- [ ] Pushed to GitHub
- [ ] CI/CD pipeline ran successfully
- [ ] Web MVC redeployed to Azure
- [ ] Checked Application Insights for warm-up messages
- [ ] Verified "4/4 services ready" in logs
- [ ] Tested feature access without home page trigger
- [ ] Confirmed all databases wake simultaneously on startup

---

## Reference Files

- **Full debugging details:** [DEBUG-WARM-UP-AZURE-ISSUE.md](DEBUG-WARM-UP-AZURE-ISSUE.md)
- **Quick diagnostic steps:** [QUICK-DIAGNOSTIC-STEPS.md](QUICK-DIAGNOSTIC-STEPS.md)
- **Implementation walkthrough:** [TEMP-WEB-PROJECT-WALKTHROUGH.md](TEMP-WEB-PROJECT-WALKTHROUGH.md)
- **Full evaluation report:** [WARMUP-IMPLEMENTATION-EVALUATION.md](WARMUP-IMPLEMENTATION-EVALUATION.md)
