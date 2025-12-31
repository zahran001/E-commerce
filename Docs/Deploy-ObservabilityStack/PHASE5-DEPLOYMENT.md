# Phase 5: Observability Infrastructure Deployment

Deploy Redis, Seq, and Jaeger as Container Apps within the existing `ecommerce-env` environment to enable full observability for all 6 microservices while maintaining a budget of ~$12-16/month.

## Key Values Reference

- **Storage Account Name:** `ecommerceobservability`
- **Storage Account Key:** [Key1 from storage account access keys]
- **File Share Name:** `seq-data`
- **Volume Mount Name:** `seq-storage`
- **Seq Active Revision:** `seq--0000007` (latest stable, minReplicas=1, runs 24/7)
- **Resource Group:** `Ecommerce-Project`
- **Region:** `eastus`

**Note on Seq Revisions:** Seq may create multiple revisions during deployment/updates. If a new revision fails (e.g., file lock errors on `stream.flare`), you can revert to the last known-good revision using `az containerapp revision activate --name seq --revision seq--0000007`. Always verify all revisions are active (not stopped) before troubleshooting further.

## Overview

| Aspect | Details |
|--------|---------|
| **Current Cost** | ~$9/month (Phase 4 microservices) |
| **Phase 5 Cost** | ~$12/month (observability stack) |
| **Total Monthly** | ~$21/month |
| **Deployment Time** | 40-50 minutes |
| **Downtime Required** | None (rolling configuration update) |
| **Code Changes** | None (configuration only) |

## What Gets Deployed

### Infrastructure Components

```
ecommerce-env (Container Apps Environment)
├── Microservices (6 apps) - EXISTING
│   ├── authapi, productapi, couponapi, shoppingcartapi, emailapi, web
│
└── Observability Stack (3 apps) - NEW
    ├── redis (0.25 CPU, 0.5GB RAM, scale-to-zero)
    ├── seq (0.25 CPU, 0.5GB RAM, 1 replica min, Azure Files mount)
    └── jaeger (0.25 CPU, 0.5GB RAM, scale-to-zero)
```

### Storage Resources

| Resource | Type | Purpose | Cost |
|----------|------|---------|------|
| `ecommerceobservability` | Storage Account | Holds Azure Files shares | ~$0.50/mo |
| `seq-data` | Azure Files Share | Seq log persistence (32GB) | ~$1.50/mo |

## Cost Breakdown

| Component | Configuration | Monthly Cost |
|-----------|---------------|--------------|
| **Redis** | 0.25 CPU, 0.5GB RAM, scale-to-zero (15% uptime) | ~$2 |
| **Seq** | 0.25 CPU, 0.5GB RAM, 1 replica (24/7) | ~$7 |
| **Jaeger** | 0.25 CPU, 0.5GB RAM, scale-to-zero (10% uptime) | ~$1 |
| **Azure Files** | 32GB quota, Standard tier | ~$1.50 |
| **Storage Account** | General Purpose v2 | ~$0.50 |
| **Observability Total** | | **~$12/month** |

## Deployment Guide

### Prerequisites

```powershell
# Verify Azure CLI is installed and authenticated
az account show

# Ensure existing Phase 4 deployment is healthy
az containerapp show --name web --resource-group Ecommerce-Project --query "properties.runningStatus"
```

### Deployment Steps

**1. Run Master Deployment Script**

```powershell
cd scripts/Prod/Phase5
.\deploy-observability-stack.ps1
```

This script automatically:
- Creates storage infrastructure (storage account + Azure Files)
- Deploys Redis, Seq, and Jaeger as Container Apps
- Updates all 6 microservices with observability configuration
- Validates deployment success

**Estimated time: 40-50 minutes**

### Individual Script Reference

If you prefer to run scripts individually:

```powershell
# Phase 5.1: Infrastructure Setup
.\01-create-storage-account.ps1
.\02-create-file-share.ps1
.\04-create-storage-mount.ps1

# Phase 5.2-5.4: Deploy observability services
.\03-deploy-redis.ps1
.\05-deploy-seq.ps1
.\06-deploy-jaeger.ps1

# Wait 60 seconds for services to stabilize
Start-Sleep -Seconds 60

# Phase 5.6: Update microservices
.\07-update-productapi-env.ps1
.\08-update-all-services-observability.ps1

# Wait 30 seconds for config propagation
Start-Sleep -Seconds 30

# Phase 5.7: Validation
.\09-validate-observability-stack.ps1
.\10-test-redis-cache.ps1
.\11-verify-seq-logs.ps1
.\12-verify-jaeger-traces.ps1
```

## Post-Deployment Verification

### 1. Infrastructure Status

```powershell
# Check all observability services are running
.\09-validate-observability-stack.ps1

# Expected output:
# Redis: Running | redis.internal.mangosea-a7508352.eastus.azurecontainerapps.io
# Seq: Running | seq.internal.mangosea-a7508352.eastus.azurecontainerapps.io
# Jaeger: Running | jaeger.internal.mangosea-a7508352.eastus.azurecontainerapps.io
```

### 2. Redis Cache Performance

```powershell
# Test cache hit performance
.\10-test-redis-cache.ps1

# Expected output:
# First call completed in 250ms
# Second call completed in 50ms
# Cache is working! 80% faster on cached response
```

### 3. Seq Log Ingestion

```powershell
# Verify all services are logging
.\11-verify-seq-logs.ps1

# Expected output:
# Found 100+ recent events
# Recent log samples:
# [2025-12-30T15:30:45.123Z] Information | AuthAPI | User login successful
# [2025-12-30T15:30:44.987Z] Information | ProductAPI | Cache hit for product_1
# [2025-12-30T15:30:44.654Z] Information | ShoppingCartAPI | Cart updated for user_123
```

### 4. Jaeger Distributed Tracing

```powershell
# Verify all services are reporting traces
.\12-verify-jaeger-traces.ps1

# Expected output:
# Found 6 services reporting traces:
#   - authapi
#   - productapi
#   - couponapi
#   - shoppingcartapi
#   - emailapi
#   - web
```

## Accessing the UIs

### Seq (Centralized Logging)

- **URL**: `https://seq.internal.mangosea-a7508352.eastus.azurecontainerapps.io`
- **Access**: Internal only (requires VPN or Azure Bastion)
- **Purpose**: Search logs, view structured data, create saved queries
- **Features**:
  - Full-text search across all logs
  - Filter by correlation ID for end-to-end tracing
  - Time-series analysis
  - Alert creation

### Jaeger (Distributed Tracing)

- **URL**: `https://jaeger.internal.mangosea-a7508352.eastus.azurecontainerapps.io`
- **Access**: Internal only (requires VPN or Azure Bastion)
- **Purpose**: Visualize request flows, identify bottlenecks
- **Features**:
  - Waterfall diagrams showing service latency
  - Span details with timing information
  - Service dependency graph
  - Error tracking across services

### Redis (Cache)

- **Connection**: `redis:6379` (internal only)
- **Purpose**: Product catalog caching, session storage
- **Access**: ProductAPI uses it automatically via IDistributedCache
- **Monitoring**: Check cache hit rates in logs

## Troubleshooting

### Redis Connection Timeout

**Symptom**: ProductAPI logs show "Connection refused" for Redis

**Diagnosis**:
```powershell
# Check Redis status
az containerapp show --name redis --resource-group Ecommerce-Project --query "properties.runningStatus"

# Check Redis logs
az containerapp logs show --name redis --resource-group Ecommerce-Project --tail 50
```

**Solution**:
- **Important**: Set Redis `minReplicas=1` to keep it always running:
  ```powershell
  az containerapp update --name redis --resource-group Ecommerce-Project --min-replicas 1
  ```
- Redis may be scaled to zero (first request wakes it up) - this causes 30-60 second delay on first use
- Verify environment variable: `CacheSettings__RedisConnection=redis:6379`
- Check all services are in same Container Apps environment
- If Redis is scale-to-zero, ProductAPI requests will timeout waiting for it to start

### Seq Not Receiving Logs

**Symptom**: Seq UI shows no events or very few recent logs

**Diagnosis**:
```powershell
# Check Seq status
az containerapp logs show --name seq --resource-group Ecommerce-Project --tail 50

# Check ProductAPI logs for Seq connection
az containerapp logs show --name productapi --resource-group Ecommerce-Project --tail 50 | Select-String "Seq"
```

**Solution**:
- Verify environment variable: `Serilog__WriteTo__2__Args__serverUrl=http://seq:80`
- Check Serilog configuration in appsettings.json includes Seq sink
- Ensure Seq container app is running (should have 1 replica minimum)

### Jaeger Not Showing Traces

**Symptom**: Jaeger UI shows "No services" or empty list

**Diagnosis**:
```powershell
# Trigger some API calls to generate traces
Invoke-WebRequest -Uri "https://web.mangosea-a7508352.eastus.azurecontainerapps.io" -Method GET

# Wait 30 seconds, then query Jaeger
$jaegerUrl = "https://jaeger.internal.mangosea-a7508352.eastus.azurecontainerapps.io"
Invoke-RestMethod -Uri "$jaegerUrl/api/services" -Method GET
```

**Solution**:
- Jaeger may be scaled to zero (first trace request wakes it up)
- Verify environment variables: `Jaeger__AgentHost=jaeger`, `Jaeger__AgentPort=6831`
- Ensure OpenTelemetry is enabled via `AddEcommerceTracing()` in Program.cs
- Generate some API traffic to create spans

### Jaeger OTLP Not Receiving Traces

**Symptom**: Using `OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317` but traces don't appear in Jaeger UI

**Diagnosis**:
```powershell
# Verify OTLP endpoint is set on each service
az containerapp show --name authapi --resource-group Ecommerce-Project --query "properties.template.containers[0].env[?name=='OTEL_EXPORTER_OTLP_ENDPOINT'].value"

# Check Jaeger ingress port mappings
az containerapp ingress show --name jaeger --resource-group Ecommerce-Project --query "exposedPort"

# Check Jaeger logs for connection attempts
az containerapp logs show --name jaeger --resource-group Ecommerce-Project --tail 100 | Select-String "4317"
```

**Solution**:
- **Verify port mappings**: Ensure Jaeger container app has ingress ports 4317 and 4318 exposed (Add these to Ingress settings → Additional Port Mappings)
- **Verify environment variable**: All 6 microservices need `OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317`
- **Check service discovery**: Apps reference `jaeger:4317` (internal hostname, not IP)
- **Wait for Jaeger startup**: Jaeger may be scale-to-zero; first OTLP request wakes it up (30-60 second delay)
- **Verify OpenTelemetry SDK**: Ensure C# apps have OpenTelemetry SDK configured with OTLP exporter (not just Jaeger agent SDK)

### Seq Data Loss After Container Restart

**Symptom**: Seq shows no historical logs after container update

**Diagnosis**:
```powershell
# Check volume mount configuration
az containerapp show --name seq --resource-group Ecommerce-Project --query "properties.template.volumes"

# Check Azure Files share has data
az storage file list --share-name seq-data --account-name ecommerceobservability --auth-mode key
```

**Solution**:
- Verify volume mount is configured correctly
- Check Azure Files share contains data
- Ensure storage mount has ReadWrite access
- If data is truly lost, it means volume mount wasn't properly configured

## Rollback (If Needed)

If Phase 5 deployment causes issues, you can roll back to Phase 4 state:

```powershell
# Remove observability stack and restore service configuration
.\rollback-observability.ps1

# Optionally delete storage account
.\rollback-observability.ps1 -DeleteStorage
```

This will:
- Delete Redis, Seq, Jaeger container apps
- Remove storage mount from environment
- Restore all 6 services to Phase 4 configuration (without observability)
- Leave microservices running unaffected

**Estimated rollback time: 5 minutes**

## Configuration Details

### Environment Variable Mapping

**Note:** Environment variables use `__` (double underscore) to override nested JSON values in appsettings.json. For example:
- `Serilog__WriteTo__2__Args__serverUrl` overrides the Seq URL in `appsettings.json` → `Serilog.WriteTo[2].Args.serverUrl`
- `Jaeger__AgentHost` overrides `appsettings.json` → `Jaeger.AgentHost`

**ProductAPI** (Redis + Seq + Jaeger):
```
CacheSettings__RedisConnection=redis:6379
CacheSettings__Enabled=true
Serilog__WriteTo__2__Args__serverUrl=http://seq:80
Jaeger__AgentHost=jaeger
Jaeger__AgentPort=6831
```

**AuthAPI, CouponAPI, ShoppingCartAPI, EmailAPI, Web** (Seq + Jaeger only):
```
Serilog__WriteTo__2__Args__serverUrl=http://seq:80
Jaeger__AgentHost=jaeger
Jaeger__AgentPort=6831
```

### OTLP Endpoint Configuration (OpenTelemetry)

**For C# apps using OpenTelemetry SDK with OTLP exporter** (all 6 microservices):

Add this environment variable to each container app:
```
OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317
```

**Note:** This uses gRPC protocol (port 4317) for better performance. Make sure to also configure ingress port mappings on Jaeger container app:
```
Port: 4317 | Exposed Port: 4317
Port: 4318 | Exposed Port: 4318
```

This allows apps to send telemetry data to Jaeger's OTLP receivers. If these ports aren't exposed, traces won't be received by Jaeger.

### Service Configuration (No Changes Required)

All services already have in their appsettings.json:

```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File"
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341"
        }
      }
    ]
  },
  "CacheSettings": {
    "Enabled": true,
    "RedisConnection": "localhost:6379"
  }
}
```

Environment variables override these defaults at deployment time.

## Performance Impact

### Redis Caching Benefits

**Before Phase 5:**
- ProductAPI hits database for every request
- Average response time: 150-200ms

**After Phase 5:**
- First request hits database, caches product list
- Subsequent requests served from Redis cache
- Average response time: 30-50ms (70-80% faster)
- Cache TTL: 3600 seconds (1 hour, configurable)

### Seq Logging Benefits

**Before Phase 5:**
- Logs scattered across 6 Container Apps
- Manual inspection required
- No centralized search

**After Phase 5:**
- All logs in Seq UI
- Full-text search capability
- Filter by service, level, correlation ID
- Structured logging with context enrichment

### Jaeger Tracing Benefits

**Before Phase 5:**
- No distributed tracing
- Request flow unknown across services
- Bottlenecks hard to identify

**After Phase 5:**
- Waterfall diagrams show service latency
- Identify slow database queries
- Track inter-service call overhead
- Correlation IDs link logs to traces

## Monitoring and Alerts

### Cost Monitoring

```powershell
# View current month's Azure costs
az consumption usage list --start-date $(Get-Date -Format "yyyy-MM-01") --end-date $(Get-Date -Format "yyyy-MM-dd")

# Create budget alert
az consumption budget create `
    --budget-name "observability-budget" `
    --amount 25 `
    --time-grain Monthly `
    --notification enabled=true threshold=80 contact-emails @("your.email@example.com")
```

### Log Queries

**Search by Correlation ID (in Seq):**
```
CorrelationId = "96ebdbee-45fa-4264-a1b8-c1be5759f40d"
```

**Find errors across all services:**
```
@Level = 'Error'
```

**Find slow ProductAPI requests:**
```
@Properties.SourceContext = 'E-commerce.Services.ProductAPI.Controllers.ProductAPIController'
AND @Level = 'Information'
AND typeof(@Properties.ElapsedMilliseconds) = 'System.Int32'
AND @Properties.ElapsedMilliseconds > 500
```

## Next Steps After Phase 5

1. **Documentation**:
   - Create `Docs/PHASE5-PROGRESS.md` documenting deployment
   - Update main README.md with Phase 5 completion status
   - Document observability setup for team onboarding

2. **Optimization**:
   - Configure Seq retention policies (7 days vs longer)
   - Fine-tune Jaeger sampling rates if needed
   - Monitor Redis cache hit rates

3. **Alerting** (Phase 6):
   - Create alerts for error rate spikes
   - Set up notifications for database connection failures
   - Configure cost anomaly detection

4. **Analysis** (Phase 6):
   - Identify performance bottlenecks
   - Optimize slow queries
   - Plan caching strategy for more endpoints

## FAQ

**Q: Can I disable Seq to save $7/month?**
A: Yes, but you'll lose centralized logging. Use `rollback-observability.ps1`. For budget-conscious setups, you could reduce Seq memory to 0.25GB (saves ~$3.50/month).

**Q: What happens if Seq runs out of storage?**
A: Seq implements automatic retention policies. Logs older than 7 days are automatically deleted to stay within 32GB quota.

**Q: How long do cached products stay in Redis?**
A: Cache TTL is 3600 seconds (1 hour, configurable). After 1 hour, ProductAPI fetches fresh data from database.

**Q: Can I view Seq/Jaeger from the public internet?**
A: No, they use internal ingress for security. You need VPN, Azure Bastion, or private network access.

**Q: What happens if services can't reach Seq/Jaeger?**
A: Serilog and OpenTelemetry include fallback behaviors. Logs go to console/file, and tracing is silently disabled. Services continue running.

**Q: Can I use Azure Cache for Redis instead of Container Apps Redis?**
A: Yes, but it costs ~$16-68/month. Container Apps Redis costs ~$2/month. Trade-off between cost and managed service benefits.

## Support

For detailed implementation information, see:
- [Detailed Phase 5 Plan](C:\Users\minha\.claude\plans\eventual-tickling-boole.md)
- [Project Architecture](CLAUDE.md)
- [Main README](README.md)

For troubleshooting scripts and detailed diagnostics, see:
- `scripts/Prod/Phase5/` - All deployment and validation scripts

---

**Last Updated**: 2025-12-30
**Status**: Ready for deployment
**Estimated Time**: 40-50 minutes
**Rollback Time**: 5 minutes
