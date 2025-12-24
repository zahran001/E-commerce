# Production-Ready Observability Implementation Plan

> **Status**: 📋 Ready for Implementation
> **Estimated Time**: 20 hours (3-4 weeks)
> **Risk Level**: Low
> **Target**: Self-hosted Seq + Jaeger on Azure VMs

## Table of Contents

1. [Overview](#overview)
2. [Current State Assessment](#current-state-assessment)
3. [Target Architecture](#target-architecture)
4. [Implementation Phases](#implementation-phases)
5. [Critical Files & Changes](#critical-files--changes)
6. [Testing Strategy](#testing-strategy)
7. [Cost Analysis](#cost-analysis)
8. [Success Criteria](#success-criteria)
9. [Rollback Plan](#rollback-plan)

---

## Overview

Transform the E-commerce microservices observability from **development-only** (localhost Seq/Jaeger) to **production-ready** self-hosted infrastructure on Azure VMs with proper security, performance, and scalability.

### Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Log Aggregation** | Self-hosted Seq on Azure VM | Full control, SQL-like queries, no vendor lock-in |
| **Distributed Tracing** | Self-hosted Jaeger on Azure VM | Unified tracing with logs, waterfall visualization |
| **Traffic Profile** | Low volume (<10k req/day) | 100% sampling acceptable, no sampling complexity |
| **Compliance** | Standard best practices | No specific regulatory requirements |
| **Implementation** | Balanced approach | Security + Observability + Performance in parallel |

---

## Current State Assessment

### ✅ What's Working (Phases 1-3)

- **Serilog structured logging** across all 5 API services
- **Correlation ID middleware** with HTTP header and Service Bus propagation
- **File-based logging** with 7-day retention
- **Database health checks** with connectivity validation
- **Seq sink** configured for localhost aggregation

### ⚠️ What's Built But Inactive (Phase 4)

- **OpenTelemetry infrastructure** in `E-commerce.Shared/Extensions/OpenTelemetryExtensions.cs`
- **Jaeger exporter** configured but not activated
- **Auto-instrumentation** ready (AspNetCore, HttpClient, EF Core, Service Bus)

### ❌ Critical Production Gaps

| Gap | Impact | Priority |
|-----|--------|----------|
| **Hardcoded localhost endpoints** | Won't work in production | CRITICAL |
| **SQL query text logging** | PII exposure risk | CRITICAL |
| **Debug.WriteLine statements** | Not captured in production | HIGH |
| **No file size limits** | Disk exhaustion risk | HIGH |
| **No appsettings.Production.json** | Configuration issues | HIGH |
| **100% trace sampling** | Cost/performance concern | MEDIUM |
| **No async sinks** | Request blocking risk | MEDIUM |

---

## Target Architecture

### Production Observability Stack

```
┌─────────────────────────────────────────────────────────────────┐
│                 Azure Container Apps (6 services)              │
│  ┌─────────────┐  ┌────────────┐  ┌─────────────┐             │
│  │   AuthAPI   │  │ ProductAPI │  │ CouponAPI   │  ...       │
│  │  - Serilog  │  │ - Serilog  │  │ - Serilog   │             │
│  │  - OTEL SDK │  │ - OTEL SDK │  │ - OTEL SDK  │             │
│  └──────┬──────┘  └──────┬─────┘  └──────┬──────┘             │
│         │                │               │                     │
│         └────────────────┼───────────────┘                     │
│                          │                                     │
│         Structured Logs (JSON)                                 │
│         OpenTelemetry Traces                                   │
└──────────────────────────┼─────────────────────────────────────┘
                           │
        ┌──────────────────┴──────────────────┐
        ↓                                      ↓
┌─────────────────────────────┐   ┌─────────────────────────────┐
│   Seq VM (Azure VM)         │   │   Jaeger VM (Azure VM)      │
│   Standard_B2s              │   │   Standard_B2s              │
│   2 vCPU, 4GB RAM           │   │   2 vCPU, 4GB RAM           │
│   128GB SSD                 │   │   64GB SSD                  │
│                             │   │                             │
│  Port: 5341 (HTTP)          │   │  Port: 6831 (UDP)           │
│  Retention: 90 days         │   │  Port: 16686 (UI)           │
│  Storage: SQL-like queries  │   │  Retention: 30 days         │
│  Cost: ~$50/month           │   │  Cost: ~$40/month           │
└─────────────────────────────┘   └─────────────────────────────┘
        ↑                                    ↑
        │ Search logs by                     │ View traces
        │ CorrelationId                      │ Waterfall charts
        └────────────────┬────────────────────┘
                         │
                  Developer Workflow:
                  1. Search correlation ID in Seq
                  2. View distributed trace in Jaeger
                  3. Identify root cause in <5 minutes
```

### Infrastructure Costs

| Resource | SKU/Size | Monthly Cost |
|----------|----------|--------------|
| **Seq VM** | Standard_B2s | ~$50 |
| **Jaeger VM** | Standard_B2s | ~$40 |
| **Networking** | Internal VNet | Free |
| **Bastion** (optional) | Standard | ~$150 |
| **Total** | | **~$90-240/month** |

**Comparison to Azure Monitor**: $2.30/GB (would be ~$20-30/month at low traffic, but no control)

---

## Implementation Phases

### Phase 1: Security Hardening (CRITICAL - 3 hours)

#### 1.1 Make SQL Query Text Logging Configurable

**File**: `E-commerce.Shared/Extensions/OpenTelemetryExtensions.cs` (line 86)

**Why**: SQL queries may contain PII (customer emails, names, addresses). Production must default to OFF.

**Change**:
```csharp
// BEFORE
.AddEntityFrameworkCoreInstrumentation(options =>
{
    options.SetDbStatementForText = true; // ❌ Always logs SQL
    options.RecordException = true;
})

// AFTER
.AddEntityFrameworkCoreInstrumentation(options =>
{
    var sqlCommandText = configuration?.GetValue<bool?>("OpenTelemetry:SqlCommandText") ?? false;
    options.SetDbStatementForText = sqlCommandText;
    options.RecordException = true;
})
```

---

#### 1.2 Remove Debug.WriteLine Statements

**File**: `E-commerce.Shared/Middleware/CorrelationIdMiddleware.cs` (lines 60, 66)

**Why**: Not visible in production, security risk if logging headers

**Change**:
```csharp
// DELETE THESE LINES:
System.Diagnostics.Debug.WriteLine($"[MIDDLEWARE] ✅ {context.Request.Method}...");
System.Diagnostics.Debug.WriteLine($"[MIDDLEWARE] 🆕 {context.Request.Method}...");
```

---

#### 1.3 Add File Size Limits

**Files**: All 5 API services `appsettings.json`
- `E-commerce.Services.AuthAPI/appsettings.json`
- `E-commerce.Services.ProductAPI/appsettings.json`
- `E-commerce.Services.CouponAPI/appsettings.json`
- `E-commerce.Services.ShoppingCartAPI/appsettings.json`
- `Ecommerce.Services.EmailAPI/appsettings.json`

**Why**: Without limits, disk exhaustion risk in containers

**Change**:
```json
{
  "Name": "File",
  "Args": {
    "path": "logs/authapi-.log",
    "rollingInterval": "Day",
    "retainedFileCountLimit": 7,
    "fileSizeLimitBytes": 104857600,
    "rollOnFileSizeLimit": true,
    "buffered": false,
    "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {CorrelationId} | {Message:lj}{NewLine}{Exception}"
  }
}
```

**Parameters**:
- `fileSizeLimitBytes: 104857600` = 100MB max per file
- `rollOnFileSizeLimit: true` = Create new file when limit exceeded
- `buffered: false` = Immediate writes (no data loss on crash)

---

### Phase 2: Production Configuration (HIGH - 2 hours)

#### 2.1 Create appsettings.Production.json

**Create 5 new files** (one per API service):
1. `E-commerce.Services.AuthAPI/appsettings.Production.json`
2. `E-commerce.Services.ProductAPI/appsettings.Production.json`
3. `E-commerce.Services.CouponAPI/appsettings.Production.json`
4. `E-commerce.Services.ShoppingCartAPI/appsettings.Production.json`
5. `Ecommerce.Services.EmailAPI/appsettings.Production.json`

**Template** (use for all services, replacing service names):

```json
{
  "Serilog": {
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
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {CorrelationId} | {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/authapi-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 104857600,
          "rollOnFileSizeLimit": true,
          "buffered": false,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {CorrelationId} | {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq-vm.internal.cloudapp.azure.com:5341"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  },
  "Jaeger": {
    "AgentHost": "jaeger-vm.internal.cloudapp.azure.com",
    "AgentPort": 6831
  },
  "OpenTelemetry": {
    "Enabled": true,
    "SamplingRatio": 1.0,
    "SqlCommandText": false
  },
  "AllowedHosts": "*"
}
```

**Key Differences from Development**:
- `MinimumLevel.Default: "Information"` (suppress Debug/Trace)
- `retainedFileCountLimit: 30` (30 days vs 7 in dev)
- `Seq.serverUrl`: Azure VM hostname (will update with actual IP)
- `Jaeger.AgentHost`: Azure VM hostname (will update with actual IP)
- `OpenTelemetry.SqlCommandText: false` (no PII)
- `OpenTelemetry.SamplingRatio: 1.0` (100% for low traffic)

---

### Phase 3: OpenTelemetry Activation (HIGH - 4 hours)

#### 3.1 Enhance OpenTelemetryExtensions.cs

**File**: `E-commerce.Shared/Extensions/OpenTelemetryExtensions.cs`

**Add configuration support for sampling, batch export, and enrichment**:

```csharp
public static IServiceCollection AddEcommerceTracing(
    this IServiceCollection services,
    string serviceName,
    string serviceVersion = "1.0.0",
    IConfiguration configuration = null)
{
    // Read configuration
    var enabled = configuration?.GetValue<bool?>("OpenTelemetry:Enabled") ?? true;
    var samplingRatio = configuration?.GetValue<double?>("OpenTelemetry:SamplingRatio") ?? 0.1;
    var sqlCommandText = configuration?.GetValue<bool?>("OpenTelemetry:SqlCommandText") ?? false;

    if (!enabled)
    {
        return services;
    }

    services.AddOpenTelemetry()
        .WithTracing(tracerProviderBuilder =>
        {
            tracerProviderBuilder
                .AddSource(serviceName)
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName, serviceVersion)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = GetEnvironment(configuration),
                            ["host.name"] = Environment.MachineName
                        }))

                // SAMPLING STRATEGY
                .SetSampler(new ParentBasedSampler(
                    new TraceIdRatioBasedSampler(samplingRatio)))

                // AUTO-INSTRUMENTATION
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = (httpContext) =>
                        !httpContext.Request.Path.StartsWithSegments("/health")
                        && !httpContext.Request.Path.StartsWithSegments("/swagger")
                        && !httpContext.Request.Path.StartsWithSegments("/healthz");

                    // Enrich with correlation ID
                    options.Enrich = (activity, eventName, rawObject) =>
                    {
                        if (rawObject is HttpRequest request)
                        {
                            var correlationId = request.HttpContext.Items["CorrelationId"]?.ToString();
                            if (!string.IsNullOrEmpty(correlationId))
                            {
                                activity.SetTag("correlation_id", correlationId);
                            }
                        }

                        // ALWAYS sample errors (override sampling ratio)
                        if (rawObject is HttpResponse response && response.StatusCode >= 400)
                        {
                            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                        }
                    };
                })

                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                })

                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.SetDbStatementForText = sqlCommandText;
                    options.RecordException = true;

                    // ALWAYS sample slow queries (>500ms)
                    options.EnrichWithIDbCommand = (activity, command) =>
                    {
                        if (activity.Duration > TimeSpan.FromMilliseconds(500))
                        {
                            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                            activity.SetTag("db.slow_query", true);
                        }
                    };
                })

                .AddSource("Azure.Messaging.ServiceBus")

                // JAEGER EXPORT WITH BATCHING
                .AddJaegerExporter(options =>
                {
                    var jaegerSection = configuration?.GetSection("Jaeger");
                    options.AgentHost = jaegerSection?.GetValue<string>("AgentHost") ?? "localhost";
                    options.AgentPort = jaegerSection?.GetValue<int>("AgentPort") ?? 6831;
                    options.MaxPayloadSizeInBytes = 4096;
                    options.ExportProcessorType = ExportProcessorType.Batch;
                    options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                    {
                        MaxQueueSize = 2048,
                        ScheduledDelayMilliseconds = 5000,
                        ExporterTimeoutMilliseconds = 30000,
                        MaxExportBatchSize = 512
                    };
                });
        });

    return services;
}
```

**Key Additions**:
- Configuration-driven sampling, SQL text, enabled flag
- ParentBasedSampler (respects upstream sampling decisions)
- Always-sample for errors (HTTP 4xx/5xx)
- Always-sample for slow queries (>500ms)
- Correlation ID enrichment from middleware
- Batch export configuration

---

#### 3.2 Activate OpenTelemetry in All Services

**Files**: All 6 `Program.cs` files
- `E-commerce.Services.AuthAPI/Program.cs`
- `E-commerce.Services.ProductAPI/Program.cs`
- `E-commerce.Services.CouponAPI/Program.cs`
- `E-commerce.Services.ShoppingCartAPI/Program.cs`
- `Ecommerce.Services.EmailAPI/Program.cs`
- `E-commerce.Web/Program.cs`

**For each service**:

**Step 1**: Add using statement
```csharp
using Ecommerce.Shared.Extensions;
```

**Step 2**: Add one-line registration (before `var app = builder.Build();`)
```csharp
builder.Services.AddEcommerceTracing("ServiceName", configuration: builder.Configuration);
```

**Service Names**:
- AuthAPI → `"AuthAPI"`
- ProductAPI → `"ProductAPI"`
- CouponAPI → `"CouponAPI"`
- ShoppingCartAPI → `"ShoppingCartAPI"`
- EmailAPI → `"EmailAPI"`
- Web MVC → `"Web"`

---

### Phase 4: Self-Hosted Infrastructure (MEDIUM - 4 hours)

#### 4.1 Deploy Seq VM on Azure

**Prerequisites**: Azure CLI authenticated

```bash
# 1. Create VM
az vm create \
  --resource-group ecommerce-rg \
  --name seq-vm \
  --image Ubuntu2204 \
  --size Standard_B2s \
  --admin-username azureuser \
  --ssh-key-value ~/.ssh/id_rsa.pub \
  --vnet-name ecommerce-vnet \
  --subnet backend-subnet

# 2. SSH into VM
ssh azureuser@seq-vm.internal.cloudapp.azure.com

# 3. Install Docker
sudo apt-get update
sudo apt-get install -y docker.io
sudo systemctl enable docker
sudo systemctl start docker

# 4. Run Seq
sudo docker run -d \
  --name seq \
  --restart unless-stopped \
  -e ACCEPT_EULA=Y \
  -v /data/seq:/data \
  -p 5341:80 \
  datalust/seq:latest

# 5. Configure retention (90 days)
# Access Seq UI → Settings → Retention → Set to 90 days
```

**Update appsettings.Production.json** with actual VM IP

---

#### 4.2 Deploy Jaeger VM on Azure

```bash
# 1. Create VM
az vm create \
  --resource-group ecommerce-rg \
  --name jaeger-vm \
  --image Ubuntu2204 \
  --size Standard_B2s \
  --admin-username azureuser \
  --ssh-key-value ~/.ssh/id_rsa.pub \
  --vnet-name ecommerce-vnet \
  --subnet backend-subnet

# 2. SSH into VM
ssh azureuser@jaeger-vm.internal.cloudapp.azure.com

# 3. Install Docker
sudo apt-get update
sudo apt-get install -y docker.io
sudo systemctl enable docker
sudo systemctl start docker

# 4. Run Jaeger with persistent storage
sudo docker run -d \
  --name jaeger \
  --restart unless-stopped \
  -e SPAN_STORAGE_TYPE=badger \
  -e BADGER_EPHEMERAL=false \
  -v /data/jaeger:/data/jaeger \
  -p 6831:6831/udp \
  -p 16686:16686 \
  jaegertracing/all-in-one:latest
```

**Update appsettings.Production.json** with actual VM IP

---

### Phase 5: Performance Optimization (MEDIUM - 2 hours)

#### 5.1 Implement Async Serilog Sinks

**Why**: Prevent logging from blocking request threads

**Step 1**: Add NuGet package
```bash
dotnet add package Serilog.Sinks.Async
```

**Step 2**: Wrap Seq sink in async configuration (appsettings.Production.json)

```json
{
  "WriteTo": [
    {
      "Name": "Async",
      "Args": {
        "configure": [
          {
            "Name": "Seq",
            "Args": {
              "serverUrl": "http://seq-vm:5341",
              "apiKey": "XXXXXXXXXXX"
            }
          }
        ],
        "bufferSize": 10000,
        "blockWhenFull": false
      }
    }
  ]
}
```

---

### Phase 6: Testing & Validation (CRITICAL - 3 hours)

#### 6.1 Local Development Testing

**Test SQL Text Logging**:
```bash
# Development mode (SQL text ON)
dotnet run --project E-commerce.Services.AuthAPI
# Register a user → Check Seq for SQL text

# Production mode (SQL text OFF)
ASPNETCORE_ENVIRONMENT=Production dotnet run
# Register a user → Check Jaeger, SQL text should NOT appear
```

**Test File Size Limits**:
```bash
# Generate logs
for i in {1..1000}; do curl https://localhost:7002/health; done

# Verify rotation
ls -lh E-commerce.Services.AuthAPI/logs/
# Should see multiple files if 100MB exceeded
```

---

#### 6.2 Production Testing (Staging)

**Deploy to Staging with Production Config**:
```bash
az containerapp update \
  --name authapi-staging \
  --resource-group ecommerce-rg \
  --set-env-vars ASPNETCORE_ENVIRONMENT=Production
```

**Verify Seq Connectivity**:
- Open Seq UI at http://<seq-vm-public-ip>:5341
- Filter by Service = "AuthAPI"
- Verify logs appearing

**Verify Jaeger Connectivity**:
- Open Jaeger UI at http://<jaeger-vm-public-ip>:16686
- Select Service: AuthAPI
- Click "Find Traces"
- Verify traces appearing with waterfall charts

---

### Phase 7: Documentation (LOW - 2 hours)

Create `Docs/OBSERVABILITY-RUNBOOK.md` with:
- Daily operations (check health, query logs)
- Common troubleshooting scenarios
- Backup/restore procedures
- Incident response guides

---

## Critical Files & Changes

### Files to Modify (15 total)

#### Security & Core (3 files)
1. `E-commerce.Shared/Extensions/OpenTelemetryExtensions.cs` - Configuration support
2. `E-commerce.Shared/Middleware/CorrelationIdMiddleware.cs` - Remove Debug.WriteLine
3. `E-commerce.Shared/E-commerce.Shared.csproj` - Add Serilog.Sinks.Async

#### Development Config (5 files)
4-8. All API services `appsettings.json` - Add file size limits

#### Production Config (6 NEW files)
9-14. All API services `appsettings.Production.json` - New files with Seq/Jaeger endpoints
15. `E-commerce.Web/appsettings.Production.json` - Update endpoints

#### OpenTelemetry Activation (Pattern across 6 files)
16-21. All `Program.cs` files - Add one-line registration

---

## Testing Strategy

### Unit Testing
✅ No breaking changes - all additions are backward compatible

### Integration Testing
1. **Local Development**: Start all services with localhost Seq/Jaeger
2. **Staging**: Deploy with production config, verify endpoints
3. **Production**: Monitor for 24 hours after deployment

### Success Metrics
- [ ] All logs appear in Seq with CorrelationId
- [ ] All traces appear in Jaeger with waterfall timing
- [ ] SQL text visible in dev, NOT visible in prod
- [ ] No performance degradation (<5% latency increase)
- [ ] File rotation working correctly

---

## Cost Analysis

### Monthly Costs

| Resource | Cost |
|----------|------|
| Seq VM (Standard_B2s) | ~$50 |
| Jaeger VM (Standard_B2s) | ~$40 |
| Internal networking | Free |
| **Total** | **~$90/month** |

**Optional**: Add Azure Bastion (~$150/month) for remote access

**Alternative**: Azure Monitor would cost ~$20-30/month at low traffic, but you lose:
- Full query language control
- SQL-like syntax (Seq is more familiar)
- No vendor lock-in
- Unlimited retention (vs 90 days default)

---

## Success Criteria

### Development
- [ ] Services start without errors
- [ ] Seq shows logs from all 6 services
- [ ] Jaeger shows traces from all 6 services
- [ ] CorrelationIds propagate end-to-end
- [ ] File logs rotate at 100MB

### Production
- [ ] Logs appear in Seq (not localhost)
- [ ] Traces appear in Jaeger (not localhost)
- [ ] SQL text NOT visible in traces
- [ ] CorrelationIds searchable in both tools
- [ ] Load test passes (100 req/s)
- [ ] No request blocking from async sinks

### Team
- [ ] Runbook created and reviewed
- [ ] Team trained on Seq queries
- [ ] Team trained on Jaeger interpretation
- [ ] Incident response documented

---

## Rollback Plan

**If OpenTelemetry causes issues**:

1. **Disable via environment variable**:
```bash
az containerapp update \
  --name <service-name> \
  --set-env-vars ENABLE_OPENTELEMETRY=false
```

2. **Revert Program.cs** (remove one-line registration)

3. **Redeploy**: Old Container App revision

**Rollback time**: <5 minutes

---

## Implementation Timeline

| Week | Phase | Hours | Deliverable |
|------|-------|-------|-------------|
| Week 1 | Security + Config | 5 | appsettings.Production.json created |
| Week 2 | Infrastructure | 4 | Seq/Jaeger VMs running on Azure |
| Week 3 | OpenTelemetry | 4 | OTEL activated in all services |
| Week 4 | Testing + Docs | 3 | Staging validated, runbook complete |
| Week 5 | Production Deploy | 2 | Live with monitoring |
| **Total** | | **18-20 hours** | **Production-Ready** |

---

## Next Steps

1. **Review this plan** with team
2. **Confirm VM naming conventions** (internal DNS)
3. **Confirm network access** (VNet configuration)
4. **Schedule implementation** (3-4 week timeline)
5. **Assign team members** (2-3 developers)

---

## Questions & Clarifications Needed

Before starting implementation:

1. **VM Hostnames**: What naming convention? (e.g., `seq-vm.internal.cloudapp.azure.com`)
2. **VNet Configuration**: Are Container Apps and VMs in same VNet?
3. **Access Method**: How will team access UIs? (Bastion, VPN, Public IP)
4. **Backup Strategy**: Where should Seq backups go? (Blob Storage)
5. **Alert Destinations**: Where should alerts go? (Email, Teams, PagerDuty)

---

## References

- **OBSERVABILITY-IMPLEMENTATION-GUIDE.md** - Original detailed guide (1600+ lines)
- **PHASE3-CORRELATION-ID-IMPLEMENTATION.md** - Correlation ID setup
- **CLAUDE.md** - Project architecture context
- **Plan file**: `.claude/plans/shiny-imagining-church.md` - Full implementation details

---

**Last Updated**: 2025-12-24
**Version**: 1.0.0
**Status**: 📋 Ready for Implementation
**Estimated Start**: Week of [TBD]
**Estimated Completion**: 3-4 weeks
