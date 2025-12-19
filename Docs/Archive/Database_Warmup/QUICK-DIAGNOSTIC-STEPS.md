# Quick Diagnostic Steps - Find the Exact Issue

## Step 1: Check Application Insights (2 minutes)

Go to your Azure Container Apps Application Insights and run this query:

```kusto
traces
| where message contains "database warm-up" or message contains "Warming up"
| order by timestamp desc
| take 50
```

### What to Look For:

**Scenario A: No warm-up messages at all**
```
[No results]
```
→ **Root Cause:** Warm-up service not running
- Check: Is DatabaseWarmUp.Enabled = true in config?
- Check: Is appsettings.Production.json overriding the config?

---

**Scenario B: Warm-up starts but all services timeout**
```
info: Starting database warm-up for 4 services
info: Warming up ProductAPI at https://localhost:7000/health
info: Warming up CouponAPI at https://localhost:7001/health
info: Warming up AuthAPI at https://localhost:7002/health
info: Warming up ShoppingCartAPI at https://localhost:7003/health
warn: ProductAPI timed out after 45000ms on attempt 1/3
warn: ProductAPI timed out after 45000ms on attempt 2/3
warn: ProductAPI timed out after 45000ms on attempt 3/3
warn: CouponAPI timed out after 45000ms on attempt 1/3
... (similar for other 3)
err: Database warm-up encountered an unexpected error
```

→ **Root Cause:** Using localhost URLs in Azure (LOCALHOST URLs DON'T WORK IN AZURE!)
- Fix: Create appsettings.Production.json with internal Azure URLs

---

**Scenario C: Warm-up starts, ProductAPI works, others timeout**
```
info: Starting database warm-up for 4 services
info: Warming up ProductAPI at https://localhost:7000/health
info: ProductAPI is ready (attempt 1/3, 5234ms)
warn: CouponAPI timed out after 45000ms on attempt 1/3
warn: AuthAPI timed out after 45000ms on attempt 1/3
warn: ShoppingCartAPI timed out after 45000ms on attempt 1/3
```

→ **Root Cause:** Still localhost issue, but somehow ProductAPI responds anyway
- Likely: ProductAPI is the only one listening, others are down or misconfigured
- Fix: Verify all 4 API containers are running and healthy

---

**Scenario D: Warm-up starts, all timeout, but you see HTTPRequestException**
```
info: Starting database warm-up for 4 services
warn: AuthAPI failed on attempt 1/3: The SSL connection could not be established
```

→ **Root Cause:** Certificate validation failure (self-signed certs)
- Fix: Add certificate validation bypass (see DEBUG-WARM-UP-AZURE-ISSUE.md)

---

**Scenario E: Everything works (IDEAL)**
```
info: Starting database warm-up for 4 services
info: Warming up ProductAPI at https://productapi.internal.{env}.azurecontainerapps.io/health
info: Warming up CouponAPI at https://couponapi.internal.{env}.azurecontainerapps.io/health
info: Warming up AuthAPI at https://authapi.internal.{env}.azurecontainerapps.io/health
info: Warming up ShoppingCartAPI at https://shoppingcart.internal.{env}.azurecontainerapps.io/health
info: ProductAPI is ready (attempt 1/5, 4523ms)
info: CouponAPI is ready (attempt 1/5, 5234ms)
info: AuthAPI is ready (attempt 1/5, 6123ms)
info: ShoppingCartAPI is ready (attempt 1/5, 4856ms)
info: Database warm-up completed: 4/4 services ready in 6234ms
```

→ **Status:** ✅ Working perfectly!

---

## Step 2: Check Container App Logs (1 minute)

In Azure Portal, go to Container Apps → Logs → Containers:

Look for any errors from Web MVC startup:
```
error: Exception while binding configuration
error: Failed to get hosted service
```

These would indicate configuration or DI issues.

---

## Step 3: Verify Your Container App URLs (2 minutes)

In Azure Portal:
1. Go to **Container Apps**
2. Select **your-webmvc-app**
3. In the left menu, click **Ingress**
4. Note the **Application URL** - it should look like:
   ```
   https://your-webmvc.{randomid}.{region}.azurecontainerapps.io
   ```

5. The **internal FQDN** format for inter-container communication is:
   ```
   https://{container-name}.internal.{same-randomid}.{region}.azurecontainerapps.io
   ```

**Example:** If Web MVC URL is `https://webmvc.mangofruit.eastus.azurecontainerapps.io`, then ProductAPI's internal URL would be:
```
https://productapi.internal.mangofruit.eastus.azurecontainerapps.io
```

---

## Step 4: Quick Fix (Apply Immediately)

### IF Scenario A or B (No messages or all timeout):

**Create `E-commerce.Web/appsettings.Production.json`:**

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
    "ProductAPI": "https://productapi.internal.{YOUR-ENV}.azurecontainerapps.io",
    "CouponAPI": "https://couponapi.internal.{YOUR-ENV}.azurecontainerapps.io",
    "AuthAPI": "https://authapi.internal.{YOUR-ENV}.azurecontainerapps.io",
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

Replace `{YOUR-ENV}` with the middle part from your Container Apps URL (the random ID like `mangofruit`).

Then:
1. Commit and push
2. Redeploy to Azure
3. Check logs again using Step 1

---

## Step 5: Verify the Fix Worked

After deploying, check Application Insights again:

```kusto
traces
| where timestamp > ago(10m)
| where message contains "Database warm-up completed"
| project timestamp, message
```

Should show:
```
[timestamp] Database warm-up completed: 4/4 services ready in XXXms
```

If you see `4/4` instead of just ProductAPI waking up later, you've fixed it! ✅

---

## Summary Table

| Symptom | Likely Cause | Quick Fix |
|---------|-------------|-----------|
| No warm-up logs | Config not loaded OR Enabled=false | Check config file |
| All 4 timeout | Localhost URLs in Azure | Create appsettings.Production.json |
| Only ProductAPI ready | Same as above | Create appsettings.Production.json |
| Certificate errors | Self-signed certs | Add cert validation bypass |
| Works locally, not Azure | Environment config | Verify ASPNETCORE_ENVIRONMENT |

---

## Next Steps

1. **Check Application Insights** (Step 1) - takes 2 minutes
2. **Identify which scenario** matches your logs
3. **Apply the quick fix** if needed
4. **Redeploy and verify** using Step 5

If you run into issues during these steps, let me know which scenario you see in Application Insights and I can provide more targeted help.
