# E-commerce Microservices - Deployment Plan

**Status:** Phase 2 Completed - Containerization ✅
**Target Platform:** Azure Container Apps + NGINX Reverse Proxy
**Estimated Monthly Cost:** ~$60-80
**Timeline:** 3-4 days
**Last Updated:** 2025-11-12

---

## Executive Summary

This deployment plan transforms the current development microservices architecture into a production-ready, cloud-native deployment suitable for a professional portfolio. The approach balances cost-effectiveness (~$60/month) with demonstrating enterprise-grade capabilities.

### Architecture Decision: Azure Container Apps

**Why Container Apps over alternatives:**
- ✅ Built-in HTTPS with free managed certificates
- ✅ Integrated with Azure services (Key Vault, App Insights)
- ✅ Auto-scaling capabilities (can scale to zero for cost savings)
- ✅ Simpler than Kubernetes but shows cloud-native expertise
- ✅ Supports both HTTP services and background workers (EmailAPI)
- ✅ Native container support without IIS/App Service overhead

**What This Demonstrates:**
- Microservices architecture with 6 independent services
- Event-driven design with Azure Service Bus
- Security best practices (secrets management, HTTPS, JWT)
- DevOps capabilities (Docker, CI/CD, infrastructure automation)
- Production awareness (health checks, logging, resilience)
- Cost optimization skills

---

## Current State Analysis

### Services Inventory

| Service | Type | Current Port | Database | Status |
|---------|------|--------------|----------|--------|
| ProductAPI | REST API | 7000 | E-commerce_Product | ✅ Ready |
| CouponAPI | REST API | 7001 | E-commerce_Coupon | ✅ Ready |
| AuthAPI | REST API | 7002 | E-commerce_Auth | ✅ Ready |
| ShoppingCartAPI | REST API | 7003 | E-commerce_ShoppingCart | ✅ Ready |
| EmailAPI | Background Service | 7298 | E-commerce_Email | ✅ Ready |
| Web (MVC) | Frontend | 7230 | None | ✅ Ready |

### Critical Security Issues (MUST FIX)

⚠️ **EXPOSED SECRETS IN SOURCE CONTROL:**

1. **Azure Service Bus Connection String**
   - Location: `Ecommerce.MessageBus/MessageBus.cs:13`
   - Location: `Ecommerce.Services.EmailAPI/appsettings.json:12`
   - Risk: Full admin access to Service Bus exposed in Git history

2. **JWT Signing Secret**
   - Location: All API `appsettings.json` files
   - Current value: "A ninja must always be prepared Scooby Dooby Doo VROOM VROOM Pumpkin Muncher"
   - Risk: Anyone can forge authentication tokens

3. **SQL Server Connection Strings**
   - Pattern: `Server=ZAHRAN;Database=...;Trusted_Connection=True`
   - Risk: Hardcoded server name, won't work in Azure

### Production Readiness Gaps

- [ ] No containerization (Dockerfiles missing)
- [ ] No health check endpoints
- [ ] No structured logging with correlation IDs
- [ ] No resilience patterns (retries, circuit breakers)
- [ ] No CI/CD pipeline
- [ ] Auto-migrations on startup (risky for production)
- [ ] Hardcoded localhost URLs for service-to-service calls
- [ ] No monitoring/observability setup

---

## Phase 1: Security Hardening (Day 1) ✅ COMPLETED

**Goal:** Remove all secrets from source control and implement secure configuration management.

**Status:** ✅ Completed on 2025-11-11
**Time Taken:** ~45 minutes
**Approach:** User Secrets for local development (Azure Key Vault deferred to Phase 4)

### 1.1 Azure Key Vault Setup

**Tasks:**
- [ ] Create Azure Key Vault resource
  ```bash
  az keyvault create \
    --name ecommerce-secrets-kv \
    --resource-group ecommerce-rg \
    --location eastus
  ```
- [ ] Store secrets in Key Vault:
  - [ ] `ServiceBusConnectionString` (from MessageBus.cs)
  - [ ] `JwtSecret` (generate new 256-bit random key)
  - [ ] `SqlConnectionString-Auth`
  - [ ] `SqlConnectionString-Product`
  - [ ] `SqlConnectionString-Coupon`
  - [ ] `SqlConnectionString-ShoppingCart`
  - [ ] `SqlConnectionString-Email`

**Generate secure JWT secret:**
```bash
# PowerShell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))
```

### 1.2 Remove Hardcoded Secrets

**Files to modify:**

- [ ] **Ecommerce.MessageBus/MessageBus.cs**
  - Remove hardcoded connection string (line 13)
  - Accept connection string via constructor parameter

- [ ] **All API appsettings.json files**
  - Remove `"Secret": "..."` from ApiSettings/JwtOptions
  - Remove connection strings (keep structure for local dev)

- [ ] **Create appsettings.Production.json** for each service
  - Add to .gitignore
  - Configure Key Vault reference pattern

### 1.3 Implement Key Vault Integration

**Add NuGet packages to all services:**
```bash
dotnet add package Azure.Identity
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
```

**Update Program.cs pattern:**
```csharp
// Add before builder.Build()
if (builder.Environment.IsProduction())
{
    var keyVaultUrl = new Uri($"https://{builder.Configuration["KeyVaultName"]}.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUrl, new DefaultAzureCredential());
}
```

**Configuration mapping:**
- Key Vault secret: `ServiceBusConnectionString` → `ServiceBusConnectionString`
- Key Vault secret: `JwtSecret` → `ApiSettings:JwtOptions:Secret`
- Key Vault secret: `SqlConnectionString-Auth` → `ConnectionStrings:DefaultConnection`

### 1.4 Environment Variables for Service URLs

**Replace hardcoded URLs:**

- [ ] **ShoppingCartAPI/appsettings.json**
  ```json
  "ServiceUrls": {
    "ProductAPI": "${PRODUCT_API_URL:https://localhost:7000}",
    "CouponAPI": "${COUPON_API_URL:https://localhost:7001}"
  }
  ```

- [ ] **Web MVC/appsettings.json**
  ```json
  "ServiceUrls": {
    "AuthAPI": "${AUTH_API_URL:https://localhost:7002}",
    "ProductAPI": "${PRODUCT_API_URL:https://localhost:7000}",
    "CouponAPI": "${COUPON_API_URL:https://localhost:7001}",
    "ShoppingCartAPI": "${CART_API_URL:https://localhost:7003}"
  }
  ```

### 1.5 Update .gitignore

- [ ] Add to .gitignore:
  ```
  appsettings.Production.json
  appsettings.*.json
  !appsettings.json
  !appsettings.Development.json
  ```

**Completion Criteria:**
- ✅ No secrets in any committed files
- ✅ All services configured with User Secrets for local development
- ✅ MessageBus.cs updated to accept configuration via IConfiguration
- ✅ All 5 services tested and working locally
- ✅ Setup script created for easy developer onboarding
- ✅ .gitignore updated to prevent future secret leaks
- ⏳ Azure Key Vault integration (deferred to Phase 4 - production deployment)

**What Was Accomplished:**
- Implemented User Secrets for all 5 microservices
- Removed hardcoded Service Bus connection string from MessageBus.cs
- Removed hardcoded JWT secrets from all appsettings.json files
- Removed hardcoded SQL connection strings from all appsettings.json files
- Created automated setup script (scripts/setup-user-secrets.ps1)
- Verified all services run locally with externalized configuration
- Committed clean code to Git (no secrets in repository)

---

## Phase 2: Containerization (Day 1-2) ✅ COMPLETED

**Goal:** Create Docker images for all services and validate with docker-compose.

**Status:** ✅ Completed on 2025-11-12
**Time Taken:** ~1.5 hours (Solo-Dev MVP approach)
**Approach:** MVP Path - Production-ready Dockerfiles without local NGINX

### What Was Accomplished

✅ **Created 6 Production-Ready Dockerfiles:**
- E-commerce.Services.ProductAPI/Dockerfile
- E-commerce.Services.CouponAPI/Dockerfile
- E-commerce.Services.AuthAPI/Dockerfile
- E-commerce.Services.ShoppingCartAPI/Dockerfile
- Ecommerce.Services.EmailAPI/Dockerfile
- E-commerce.Web/Dockerfile

✅ **Multi-Stage Build Pattern:**
- Base image: mcr.microsoft.com/dotnet/aspnet:8.0
- Build with: mcr.microsoft.com/dotnet/sdk:8.0
- Optimized final images (~220-250 MB each)
- Health check endpoints configured
- Exposed on port 8080 (container standard)

✅ **Created docker-compose.yml:**
- Solo-Dev MVP approach using host.docker.internal
- Connects to existing local SQL Server (no SQL container needed)
- Environment variables configured via .env file
- Service dependencies defined
- Port mappings: 7000-7003, 7230, 7298

✅ **Build & Validation:**
- All 6 services build successfully
- Images tested and validated
- Services connect to local SQL Server via host.docker.internal
- Environment variables externalized

✅ **Documentation:**
- [PHASE2.md](PHASE2.md) - Complete guide with MVP and Full approaches
- [PHASE2-STEPS.md](PHASE2-STEPS.md) - Step-by-step progress tracker (completed)
- [../AZURE-ENV-VARS.md](../AZURE-ENV-VARS.md) - Environment variables reference for Azure deployment

### Solo-Dev MVP Approach (Chosen Path)

**Two Approaches Available:**

| Approach | Time | What You Build | Best For |
|----------|------|----------------|----------|
| **Solo-Dev MVP** ✅ | 1-1.5 hours | Dockerfiles + minimal compose | Fast deployment to Azure |
| **Full Guide** | 3-4 hours | Complete local environment | Learning, team setups |

**Key Decision: NGINX or Not?**

- **Without NGINX (MVP):** Azure Container Apps provides built-in ingress, routing, and SSL ✅ **Chosen**
- **With NGINX (Full):** Custom API gateway with advanced routing and rate limiting

**Why MVP Works:**
- ✅ Azure Container Apps already provides what NGINX offers (routing, SSL, load balancing)
- ✅ Simpler deployment and debugging
- ✅ Same portfolio value (demonstrates containerization expertise)
- ✅ Can upgrade to NGINX later without changing the Dockerfiles

**What Was Skipped in MVP:**
- ❌ NGINX reverse proxy container (Azure provides ingress)
- ❌ SQL Server container (using local SQL, then Azure SQL)
- ❌ Complex Docker networking (auto-handled)
- ❌ Extensive local E2E testing (will validate in Azure)

**What We Kept in MVP:**
- ✅ Production-ready multi-stage Dockerfiles
- ✅ Environment-based configuration
- ✅ docker-compose for local validation
- ✅ All services containerized
- ✅ Health check configuration

### Reference Files

**Complete Phase 2 Documentation:**
- [PHASE2.md](PHASE2.md) - Full containerization guide
- [PHASE2-STEPS.md](PHASE2-STEPS.md) - Step-by-step progress tracker
- [../AZURE-ENV-VARS.md](../AZURE-ENV-VARS.md) - Environment variables for Azure (moved to root for easy reference)

**See [PHASE2.md](PHASE2.md) for detailed instructions on both approaches.**

### 2.1 Create Base Dockerfile Template

**Create: `Dockerfile.template` (for reference)**

```dockerfile
# Multi-stage build for .NET 8 microservice
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ServiceName/ServiceName.csproj", "ServiceName/"]
# Add MessageBus reference if needed
RUN dotnet restore "ServiceName/ServiceName.csproj"
COPY . .
WORKDIR "/src/ServiceName"
RUN dotnet build "ServiceName.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ServiceName.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ServiceName.dll"]
```

### 2.2 Create Dockerfiles for Each Service

**Tasks:**
- [ ] **ProductAPI/Dockerfile**
  - Base image: dotnet/aspnet:8.0
  - Expose port 8080
  - Include health check: `HEALTHCHECK CMD curl --fail http://localhost:8080/health || exit 1`

- [ ] **CouponAPI/Dockerfile**
  - Same pattern as ProductAPI

- [ ] **AuthAPI/Dockerfile**
  - Include Identity dependencies
  - Same port/health check pattern

- [ ] **ShoppingCartAPI/Dockerfile**
  - Reference Ecommerce.MessageBus project
  - Same pattern

- [ ] **EmailAPI/Dockerfile**
  - Background service (no HTTP exposure)
  - Health check based on Service Bus connectivity

- [ ] **Web MVC/Dockerfile**
  - Include wwwroot static files
  - Expose port 8080

### 2.3 Update Services for Container Environment

**Common changes for all APIs:**

- [ ] **Modify Program.cs to use port 8080:**
  ```csharp
  // Remove Kestrel configuration or update to:
  builder.WebHost.ConfigureKestrel(options =>
  {
      options.ListenAnyIP(8080); // Container standard port
  });
  ```

- [ ] **Update CORS policies for production**
- [ ] **Configure forwarded headers** (behind NGINX):
  ```csharp
  app.UseForwardedHeaders(new ForwardedHeadersOptions
  {
      ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
  });
  ```

### 2.4 Create docker-compose.yml for Local Testing

**Create: `docker-compose.yml`**

```yaml
version: '3.8'

services:
  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong!Passw0rd
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

  authapi:
    build:
      context: .
      dockerfile: E-commerce.Services.AuthAPI/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_Auth;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
    ports:
      - "7002:8080"
    depends_on:
      - sql-server

  productapi:
    build:
      context: .
      dockerfile: E-commerce.Services.ProductAPI/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_Product;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
    ports:
      - "7000:8080"
    depends_on:
      - sql-server

  couponapi:
    build:
      context: .
      dockerfile: E-commerce.Services.CouponAPI/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_Coupon;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
    ports:
      - "7001:8080"
    depends_on:
      - sql-server

  shoppingcartapi:
    build:
      context: .
      dockerfile: E-commerce.Services.ShoppingCartAPI/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_ShoppingCart;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
      - ServiceUrls__ProductAPI=http://productapi:8080
      - ServiceUrls__CouponAPI=http://couponapi:8080
    ports:
      - "7003:8080"
    depends_on:
      - sql-server
      - productapi
      - couponapi

  emailapi:
    build:
      context: .
      dockerfile: Ecommerce.Services.EmailAPI/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_Email;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
      - ServiceBusConnectionString=${AZURE_SERVICEBUS_CONNECTION}
    depends_on:
      - sql-server

  web:
    build:
      context: .
      dockerfile: E-commerce.Web/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ServiceUrls__AuthAPI=http://authapi:8080
      - ServiceUrls__ProductAPI=http://productapi:8080
      - ServiceUrls__CouponAPI=http://couponapi:8080
      - ServiceUrls__ShoppingCartAPI=http://shoppingcartapi:8080
    ports:
      - "7230:8080"
    depends_on:
      - authapi
      - productapi
      - couponapi
      - shoppingcartapi

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
    depends_on:
      - web
      - authapi
      - productapi
      - couponapi
      - shoppingcartapi

volumes:
  sqldata:
```

### 2.5 Create NGINX Configuration

**Create: `nginx/nginx.conf`**

```nginx
events {
    worker_connections 1024;
}

http {
    upstream web {
        server web:8080;
    }

    upstream authapi {
        server authapi:8080;
    }

    upstream productapi {
        server productapi:8080;
    }

    upstream couponapi {
        server couponapi:8080;
    }

    upstream shoppingcartapi {
        server shoppingcartapi:8080;
    }

    server {
        listen 80;
        server_name localhost;

        # API Gateway Pattern
        location /api/auth/ {
            proxy_pass http://authapi/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        location /api/product/ {
            proxy_pass http://productapi/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        location /api/coupon/ {
            proxy_pass http://couponapi/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        location /api/cart/ {
            proxy_pass http://shoppingcartapi/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # Frontend (default)
        location / {
            proxy_pass http://web/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

### 2.6 Test Local Container Deployment

**Commands:**
```bash
# Build all images
docker-compose build

# Start all services
docker-compose up -d

# Check health
docker-compose ps

# View logs
docker-compose logs -f

# Test endpoints
curl http://localhost/api/product/
curl http://localhost/api/auth/

# Cleanup
docker-compose down
```

**Testing checklist:**
- [ ] All containers start successfully
- [ ] Database migrations auto-apply
- [ ] APIs respond through NGINX
- [ ] Service-to-service calls work (ShoppingCart → Product/Coupon)
- [ ] JWT authentication works
- [ ] Service Bus messages flow to EmailAPI

**Completion Criteria:**
- ✅ All 6 services containerized
- ✅ docker-compose successfully orchestrates full system
- ✅ NGINX routes requests correctly
- ✅ No hardcoded localhost references

---

## Phase 3: Production-Ready Enhancements (Day 2) ⚠️ PHASE 3-LITE (MVP)

**Goal:** Add observability, health checks, and resilience patterns.

**Status:** ⚠️ Phase 3-Lite Completed on 2025-11-12 (Basic health checks only)
**Time Taken:** ~15 minutes
**Approach:** MVP - Basic health endpoints only, skip advanced observability

### MVP Decision: Phase 3-Lite Approach

**Decision:** Skip most of Phase 3, add only basic `/health` endpoints for Azure Container Apps health probes.

**Reasoning:**

1. **Azure Container Apps provides Phase 3 features out-of-the-box:**
   - ✅ Built-in health probes (liveness/readiness)
   - ✅ Automatic restarts on failure
   - ✅ Log aggregation via Azure Monitor
   - ✅ Application Insights integration (one-click enable)

2. **Time efficiency for MVP:**
   - Full Phase 3: 2-3 hours
   - Phase 3-Lite: 15 minutes
   - **Savings: 2+ hours** → Better invested in Phase 4 deployment

3. **Advanced features can be added post-deployment:**
   - Serilog: Azure Monitor logs are sufficient initially
   - Correlation IDs: Add when debugging distributed tracing issues
   - Polly resilience: Add when cascade failures occur
   - Advanced health checks: Basic endpoints enough for container health probes

4. **Portfolio value:**
   - Employers care more about "Did you deploy to production?" than local logging setup
   - Can demonstrate observability using Azure's built-in tools
   - Shows pragmatic decision-making (MVP mindset)

### What Was Completed (Phase 3-Lite)

✅ **Basic Health Check Endpoints Added:**
- ProductAPI: `GET /health` → Returns `{ status: "healthy", service: "ProductAPI", timestamp: ... }`
- CouponAPI: `GET /health` → Returns `{ status: "healthy", service: "CouponAPI", timestamp: ... }`
- AuthAPI: `GET /health` → Returns `{ status: "healthy", service: "AuthAPI", timestamp: ... }`
- ShoppingCartAPI: `GET /health` → Returns `{ status: "healthy", service: "ShoppingCartAPI", timestamp: ... }`
- EmailAPI: `GET /health` → Returns `{ status: "healthy", service: "EmailAPI", timestamp: ... }`
- Web MVC: `GET /health` → Returns `{ status: "healthy", service: "WebMVC", timestamp: ... }`

**Implementation:**
```csharp
// Added to each service's Program.cs (before app.Run())
app.MapGet("/health", () => Results.Ok(new {
    status = "healthy",
    service = "ServiceName",
    timestamp = DateTime.UtcNow
}));
```

**Why this is sufficient:**
- Azure Container Apps health probes need an HTTP 200 response
- Endpoint confirms service is running and responding
- No additional packages required (minimal dependency)
- Can be enhanced later with database connectivity checks if needed

### What Was Postponed (Full Phase 3)

The following will be added **after first Azure deployment** (if needed):

❌ **Structured Logging (Serilog):**
- Reason: Azure Monitor provides built-in log aggregation
- Add later if custom log formatting is required

❌ **Correlation IDs:**
- Reason: Application Insights handles distributed tracing automatically
- Add later if manual correlation is needed

❌ **Application Insights SDK:**
- Reason: Will be configured during Azure deployment (Phase 4)
- Native integration with Container Apps

❌ **Resilience Patterns (Polly):**
- Reason: No observed cascade failures yet
- Add later when specific failure patterns emerge

❌ **Advanced Health Checks:**
- Reason: Basic endpoint sufficient for container health probes
- Add database/Service Bus checks later if needed

### When to Add Full Phase 3 Features

**After Phase 4 deployment, add these if you encounter:**

| Problem | Solution |
|---------|----------|
| Difficult to trace requests across services | Add correlation IDs + Serilog |
| ShoppingCartAPI fails when ProductAPI is slow | Add Polly retry/circuit breaker |
| Need custom log queries | Add Serilog with structured logging |
| Health probe doesn't detect database issues | Enhance /health with EF Core health checks |
| Want better telemetry | Add Application Insights custom events |

**Estimated time to add later:** 1-2 hours (per feature as needed)

---

### Original Phase 3 Plan (For Reference)

### 3.1 Health Check Endpoints

**Add to all services:**

**NuGet packages:**
```bash
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
dotnet add package AspNetCore.HealthChecks.SqlServer
```

**Update Program.cs (all APIs):**
```csharp
// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "sql-server",
        timeout: TimeSpan.FromSeconds(3)
    );

// Map endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

**EmailAPI (Service Bus health check):**
```bash
dotnet add package AspNetCore.HealthChecks.AzureServiceBus
```

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddAzureServiceBusQueue(
        builder.Configuration["ServiceBusConnectionString"],
        "emailshoppingcart",
        name: "servicebus"
    );
```

**Tasks:**
- [ ] Add health checks to ProductAPI
- [ ] Add health checks to CouponAPI
- [ ] Add health checks to AuthAPI
- [ ] Add health checks to ShoppingCartAPI
- [ ] Add health checks to EmailAPI
- [ ] Update Dockerfiles with HEALTHCHECK command
- [ ] Test health endpoints locally

### 3.2 Structured Logging with Serilog

**Add to all services:**

**NuGet packages:**
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Enrichers.CorrelationId
dotnet add package Serilog.Settings.Configuration
```

**Update Program.cs:**
```csharp
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .Enrich.WithProperty("ServiceName", "ProductAPI") // Change per service
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();
```

**Add to appsettings.json:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

**Add correlation ID middleware:**
```csharp
// In Program.cs, before app.UseAuthentication()
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers.Add("X-Correlation-ID", correlationId);
    await next();
});
```

**Tasks:**
- [ ] Add Serilog to all 6 services
- [ ] Configure JSON output for Container Apps
- [ ] Test correlation ID propagation
- [ ] Verify structured logs in console

### 3.3 Application Insights Integration

**Add to all services:**

**NuGet package:**
```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

**Update Program.cs:**
```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});
```

**Add to appsettings.Production.json:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "${APPINSIGHTS_CONNECTION_STRING}"
  }
}
```

**Tasks:**
- [ ] Create Application Insights resource in Azure
- [ ] Add AI to all services
- [ ] Configure dependency tracking
- [ ] Test telemetry flow

### 3.4 Resilience Patterns with Polly

**Add to ShoppingCartAPI (calls ProductAPI/CouponAPI):**

**NuGet packages:**
```bash
dotnet add package Microsoft.Extensions.Http.Resilience
```

**Update Program.cs (ShoppingCartAPI):**
```csharp
// Replace existing HttpClient registration
builder.Services.AddHttpClient("Product", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:ProductAPI"]);
})
.AddHttpMessageHandler<BackendAPIAuthenticationHttpClientHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry = new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential
    };
    options.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
    {
        SamplingDuration = TimeSpan.FromSeconds(10),
        FailureRatio = 0.5,
        MinimumThroughput = 3,
        BreakDuration = TimeSpan.FromSeconds(30)
    };
    options.AttemptTimeout = new HttpTimeoutStrategyOptions
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
});

// Same for Coupon HttpClient
builder.Services.AddHttpClient("Coupon", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:CouponAPI"]);
})
.AddHttpMessageHandler<BackendAPIAuthenticationHttpClientHandler>()
.AddStandardResilienceHandler();
```

**Tasks:**
- [ ] Add Polly to ShoppingCartAPI
- [ ] Test retry behavior (simulate ProductAPI failure)
- [ ] Test circuit breaker (sustained failures)
- [ ] Verify timeout policies

**Completion Criteria:**
- ✅ All services have /health endpoints
- ✅ Structured JSON logging enabled
- ✅ Correlation IDs propagate across services
- ✅ Application Insights collecting telemetry
- ✅ Retry/circuit breaker policies active

---

## Phase 4: Azure Infrastructure Setup (Day 3)

**Goal:** Provision Azure resources and deploy containerized services.

### 4.1 Create Azure Resources

**Prerequisites:**
- [ ] Azure subscription active
- [ ] Azure CLI installed and logged in
- [ ] Resource group created

**Azure CLI commands:**

```bash
# Login
az login

# Set subscription
az account set --subscription "Your-Subscription-Name"

# Create resource group
az group create --name ecommerce-rg --location eastus

# Create Azure Container Registry
az acr create \
  --name ecommerceacr \
  --resource-group ecommerce-rg \
  --sku Basic \
  --admin-enabled true

# Get ACR credentials
az acr credential show --name ecommerceacr

# Create Key Vault
az keyvault create \
  --name ecommerce-secrets-kv \
  --resource-group ecommerce-rg \
  --location eastus \
  --enable-rbac-authorization false

# Create Application Insights
az monitor app-insights component create \
  --app ecommerce-insights \
  --location eastus \
  --resource-group ecommerce-rg \
  --application-type web

# Get instrumentation key
az monitor app-insights component show \
  --app ecommerce-insights \
  --resource-group ecommerce-rg \
  --query instrumentationKey
```

**Tasks:**
- [ ] Create resource group
- [ ] Create Azure Container Registry
- [ ] Create Key Vault
- [ ] Create Application Insights
- [ ] Note down all resource IDs/keys

### 4.2 Setup Azure SQL Database

**Option A: Single Database with Multiple Schemas (Recommended - ~$5/month)**

```bash
# Create SQL Server
az sql server create \
  --name ecommerce-sql-server \
  --resource-group ecommerce-rg \
  --location eastus \
  --admin-user sqladmin \
  --admin-password 'YourStrong!Password123'

# Configure firewall for Azure services
az sql server firewall-rule create \
  --resource-group ecommerce-rg \
  --server ecommerce-sql-server \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Create single database (Serverless tier)
az sql db create \
  --resource-group ecommerce-rg \
  --server ecommerce-sql-server \
  --name ecommerce-db \
  --edition GeneralPurpose \
  --compute-model Serverless \
  --family Gen5 \
  --capacity 1 \
  --auto-pause-delay 60
```

**Schema strategy:**
- `dbo.Auth_*` tables (AspNetUsers, AspNetRoles, etc.)
- `dbo.Product_*` tables (Products)
- `dbo.Coupon_*` tables (Coupons)
- `dbo.Cart_*` tables (CartHeaders, CartDetails)
- `dbo.Email_*` tables (EmailLoggers)

**Update connection strings to use single database:**
```
Server=tcp:ecommerce-sql-server.database.windows.net,1433;
Database=ecommerce-db;
User ID=sqladmin;
Password=YourStrong!Password123;
Encrypt=True;
TrustServerCertificate=False;
Connection Timeout=30;
```

**Option B: Separate Databases (~$25/month)**

```bash
# Create 5 separate databases
for db in auth product coupon shoppingcart email; do
  az sql db create \
    --resource-group ecommerce-rg \
    --server ecommerce-sql-server \
    --name ecommerce-${db} \
    --edition Basic
done
```

**Tasks:**
- [ ] Create SQL Server
- [ ] Choose single vs. multiple database approach
- [ ] Create database(s)
- [ ] Configure firewall rules
- [ ] Test connectivity from local machine
- [ ] Store connection string in Key Vault

### 4.3 Store Secrets in Key Vault

```bash
# Store Service Bus connection string
az keyvault secret set \
  --vault-name ecommerce-secrets-kv \
  --name ServiceBusConnectionString \
  --value "Endpoint=sb://ecommerceweb.servicebus.windows.net/;..."

# Store JWT secret (generate new one!)
az keyvault secret set \
  --vault-name ecommerce-secrets-kv \
  --name JwtSecret \
  --value "$(openssl rand -base64 64)"

# Store SQL connection string
az keyvault secret set \
  --vault-name ecommerce-secrets-kv \
  --name SqlConnectionString \
  --value "Server=tcp:ecommerce-sql-server.database.windows.net,1433;..."

# Get Application Insights connection string
AI_CONN=$(az monitor app-insights component show \
  --app ecommerce-insights \
  --resource-group ecommerce-rg \
  --query connectionString -o tsv)

az keyvault secret set \
  --vault-name ecommerce-secrets-kv \
  --name AppInsightsConnectionString \
  --value "$AI_CONN"
```

**Tasks:**
- [ ] Generate new JWT secret (256-bit minimum)
- [ ] Store Service Bus connection string
- [ ] Store JWT secret
- [ ] Store SQL connection string(s)
- [ ] Store Application Insights connection string
- [ ] Verify secrets readable

### 4.4 Build and Push Docker Images

```bash
# Login to ACR
az acr login --name ecommerceacr

# Build and push all images
docker build -t ecommerceacr.azurecr.io/authapi:latest \
  -f E-commerce.Services.AuthAPI/Dockerfile .
docker push ecommerceacr.azurecr.io/authapi:latest

docker build -t ecommerceacr.azurecr.io/productapi:latest \
  -f E-commerce.Services.ProductAPI/Dockerfile .
docker push ecommerceacr.azurecr.io/productapi:latest

docker build -t ecommerceacr.azurecr.io/couponapi:latest \
  -f E-commerce.Services.CouponAPI/Dockerfile .
docker push ecommerceacr.azurecr.io/couponapi:latest

docker build -t ecommerceacr.azurecr.io/shoppingcartapi:latest \
  -f E-commerce.Services.ShoppingCartAPI/Dockerfile .
docker push ecommerceacr.azurecr.io/shoppingcartapi:latest

docker build -t ecommerceacr.azurecr.io/emailapi:latest \
  -f Ecommerce.Services.EmailAPI/Dockerfile .
docker push ecommerceacr.azurecr.io/emailapi:latest

docker build -t ecommerceacr.azurecr.io/web:latest \
  -f E-commerce.Web/Dockerfile .
docker push ecommerceacr.azurecr.io/web:latest

# Verify images
az acr repository list --name ecommerceacr --output table
```

**Tasks:**
- [ ] Build all 6 Docker images
- [ ] Tag with ACR registry name
- [ ] Push to Azure Container Registry
- [ ] Verify images uploaded successfully

### 4.5 Create Azure Container Apps Environment

```bash
# Install Container Apps extension
az extension add --name containerapp --upgrade

# Create Container Apps environment
az containerapp env create \
  --name ecommerce-env \
  --resource-group ecommerce-rg \
  --location eastus

# Get ACR password
ACR_PASSWORD=$(az acr credential show \
  --name ecommerceacr \
  --query 'passwords[0].value' -o tsv)

# Create Container Apps (ProductAPI example)
az containerapp create \
  --name productapi \
  --resource-group ecommerce-rg \
  --environment ecommerce-env \
  --image ecommerceacr.azurecr.io/productapi:latest \
  --registry-server ecommerceacr.azurecr.io \
  --registry-username ecommerceacr \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress internal \
  --min-replicas 1 \
  --max-replicas 1 \
  --cpu 0.5 \
  --memory 1Gi \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    KeyVaultName=ecommerce-secrets-kv

# Repeat for CouponAPI, AuthAPI, ShoppingCartAPI, EmailAPI

# Create Web MVC with external ingress
az containerapp create \
  --name web \
  --resource-group ecommerce-rg \
  --environment ecommerce-env \
  --image ecommerceacr.azurecr.io/web:latest \
  --registry-server ecommerceacr.azurecr.io \
  --registry-username ecommerceacr \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 2 \
  --cpu 0.5 \
  --memory 1Gi \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    KeyVaultName=ecommerce-secrets-kv \
    ServiceUrls__AuthAPI=https://authapi.internal.{env-url} \
    ServiceUrls__ProductAPI=https://productapi.internal.{env-url} \
    ServiceUrls__CouponAPI=https://couponapi.internal.{env-url} \
    ServiceUrls__ShoppingCartAPI=https://shoppingcartapi.internal.{env-url}
```

### 4.6 Configure Managed Identity for Key Vault Access

```bash
# Enable managed identity for each Container App
for app in productapi couponapi authapi shoppingcartapi emailapi web; do
  az containerapp identity assign \
    --name $app \
    --resource-group ecommerce-rg \
    --system-assigned
done

# Get managed identity principal ID (example for productapi)
PRINCIPAL_ID=$(az containerapp identity show \
  --name productapi \
  --resource-group ecommerce-rg \
  --query principalId -o tsv)

# Grant Key Vault access to managed identity
az keyvault set-policy \
  --name ecommerce-secrets-kv \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list

# Repeat for all other services
```

**Tasks:**
- [ ] Create Container Apps environment
- [ ] Deploy all 6 Container Apps
- [ ] Configure internal ingress for APIs
- [ ] Configure external ingress for Web MVC
- [ ] Enable managed identities
- [ ] Grant Key Vault access to all identities
- [ ] Set environment variables for service URLs

### 4.7 Run Database Migrations

**Option 1: Manual (one-time)**
```bash
# From local machine, update connection string to Azure SQL
# Run migrations for each service
cd E-commerce.Services.AuthAPI
dotnet ef database update

cd ../E-commerce.Services.ProductAPI
dotnet ef database update

# ... repeat for all services
```

**Option 2: Init Container (recommended for production)**
- Create separate migration job Container App
- Runs once on deployment
- Uses EF Core Bundle

**Tasks:**
- [ ] Update connection strings to Azure SQL
- [ ] Run migrations for all 5 databases
- [ ] Verify seed data created
- [ ] Test database connectivity from Container Apps

**Completion Criteria:**
- ✅ All Azure resources provisioned
- ✅ Docker images in Container Registry
- ✅ All Container Apps running
- ✅ Managed identities configured
- ✅ Databases migrated and seeded
- ✅ Secrets accessible from Key Vault

---

## Phase 5: NGINX API Gateway (Day 3)

**Goal:** Deploy NGINX as reverse proxy for API gateway pattern and SSL termination.

### 5.1 Create NGINX Production Configuration

**Create: `nginx/nginx.production.conf`**

```nginx
events {
    worker_connections 2048;
}

http {
    # Logging
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for" '
                    'rt=$request_time uct="$upstream_connect_time" '
                    'uht="$upstream_header_time" urt="$upstream_response_time"';

    access_log /var/log/nginx/access.log main;
    error_log /var/log/nginx/error.log warn;

    # Upstreams (internal Container Apps URLs)
    upstream web {
        server web.internal.<ENV-SUFFIX>:8080;
    }

    upstream authapi {
        server authapi.internal.<ENV-SUFFIX>:8080;
    }

    upstream productapi {
        server productapi.internal.<ENV-SUFFIX>:8080;
    }

    upstream couponapi {
        server couponapi.internal.<ENV-SUFFIX>:8080;
    }

    upstream shoppingcartapi {
        server shoppingcartapi.internal.<ENV-SUFFIX>:8080;
    }

    # Rate limiting
    limit_req_zone $binary_remote_addr zone=api_limit:10m rate=100r/s;
    limit_req_zone $binary_remote_addr zone=auth_limit:10m rate=10r/s;

    # HTTP server (redirect to HTTPS)
    server {
        listen 80;
        server_name _;
        return 301 https://$host$request_uri;
    }

    # HTTPS server
    server {
        listen 443 ssl http2;
        server_name your-domain.com;

        # SSL configuration (Container Apps provides managed cert)
        ssl_certificate /etc/nginx/ssl/cert.pem;
        ssl_certificate_key /etc/nginx/ssl/key.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers HIGH:!aNULL:!MD5;
        ssl_prefer_server_ciphers on;

        # Security headers
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header X-XSS-Protection "1; mode=block" always;
        add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

        # API Gateway routes with rate limiting
        location /api/auth/ {
            limit_req zone=auth_limit burst=20 nodelay;

            proxy_pass http://authapi/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Correlation-ID $request_id;

            # Timeouts
            proxy_connect_timeout 10s;
            proxy_send_timeout 30s;
            proxy_read_timeout 30s;
        }

        location /api/product/ {
            limit_req zone=api_limit burst=50 nodelay;

            proxy_pass http://productapi/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Correlation-ID $request_id;
        }

        location /api/coupon/ {
            limit_req zone=api_limit burst=50 nodelay;

            proxy_pass http://couponapi/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Correlation-ID $request_id;
        }

        location /api/cart/ {
            limit_req zone=api_limit burst=50 nodelay;

            proxy_pass http://shoppingcartapi/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Correlation-ID $request_id;
        }

        # Health check endpoint
        location /health {
            access_log off;
            return 200 "healthy\n";
            add_header Content-Type text/plain;
        }

        # Frontend (default route)
        location / {
            proxy_pass http://web/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Correlation-ID $request_id;

            # WebSocket support (if needed)
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
        }
    }
}
```

### 5.2 Create NGINX Dockerfile

**Create: `nginx/Dockerfile`**

```dockerfile
FROM nginx:alpine

# Copy configuration
COPY nginx/nginx.production.conf /etc/nginx/nginx.conf

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost/health || exit 1

EXPOSE 80 443

CMD ["nginx", "-g", "daemon off;"]
```

### 5.3 Deploy NGINX to Container Apps

```bash
# Build and push NGINX image
docker build -t ecommerceacr.azurecr.io/nginx:latest -f nginx/Dockerfile .
docker push ecommerceacr.azurecr.io/nginx:latest

# Deploy NGINX Container App
az containerapp create \
  --name nginx-gateway \
  --resource-group ecommerce-rg \
  --environment ecommerce-env \
  --image ecommerceacr.azurecr.io/nginx:latest \
  --registry-server ecommerceacr.azurecr.io \
  --registry-username ecommerceacr \
  --registry-password "$ACR_PASSWORD" \
  --target-port 80 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 3 \
  --cpu 0.25 \
  --memory 0.5Gi

# Get NGINX public URL
az containerapp show \
  --name nginx-gateway \
  --resource-group ecommerce-rg \
  --query properties.configuration.ingress.fqdn -o tsv
```

### 5.4 Configure Custom Domain and SSL (Optional)

**If you have a custom domain:**

```bash
# Add custom domain
az containerapp hostname add \
  --hostname your-domain.com \
  --resource-group ecommerce-rg \
  --name nginx-gateway

# Bind managed certificate (free)
az containerapp hostname bind \
  --hostname your-domain.com \
  --resource-group ecommerce-rg \
  --name nginx-gateway \
  --environment ecommerce-env \
  --validation-method CNAME
```

**DNS Configuration:**
- Add CNAME record: `your-domain.com` → `nginx-gateway.{region}.azurecontainerapps.io`
- Wait for DNS propagation (can take up to 48 hours)

**Tasks:**
- [ ] Create NGINX production configuration
- [ ] Build NGINX Docker image
- [ ] Push to Container Registry
- [ ] Deploy NGINX Container App with external ingress
- [ ] Get public URL
- [ ] Test API routing through NGINX
- [ ] (Optional) Configure custom domain
- [ ] (Optional) Enable managed SSL certificate

**Completion Criteria:**
- ✅ NGINX routing all requests correctly
- ✅ HTTPS enabled (self-signed or managed cert)
- ✅ Rate limiting active
- ✅ Correlation IDs propagating
- ✅ Security headers present

---

## Phase 6: CI/CD Pipeline (Day 4)

**Goal:** Automate build and deployment process with GitHub Actions.

### 6.1 Create GitHub Secrets

**Required secrets (add in GitHub repo settings):**

```
AZURE_CREDENTIALS          # Service principal JSON
ACR_USERNAME               # ecommerceacr
ACR_PASSWORD               # From az acr credential show
AZURE_SUBSCRIPTION_ID      # Your subscription ID
AZURE_RESOURCE_GROUP       # ecommerce-rg
ACR_LOGIN_SERVER           # ecommerceacr.azurecr.io
```

**Create service principal:**
```bash
az ad sp create-for-rbac \
  --name "github-actions-ecommerce" \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/ecommerce-rg \
  --sdk-auth
```

### 6.2 Create GitHub Actions Workflow

**Create: `.github/workflows/deploy.yml`**

```yaml
name: Build and Deploy

on:
  push:
    branches: [ master ]
  pull_request:
    branches: [ master ]
  workflow_dispatch:

env:
  ACR_LOGIN_SERVER: ${{ secrets.ACR_LOGIN_SERVER }}
  RESOURCE_GROUP: ${{ secrets.AZURE_RESOURCE_GROUP }}

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        service:
          - name: authapi
            dockerfile: E-commerce.Services.AuthAPI/Dockerfile
          - name: productapi
            dockerfile: E-commerce.Services.ProductAPI/Dockerfile
          - name: couponapi
            dockerfile: E-commerce.Services.CouponAPI/Dockerfile
          - name: shoppingcartapi
            dockerfile: E-commerce.Services.ShoppingCartAPI/Dockerfile
          - name: emailapi
            dockerfile: Ecommerce.Services.EmailAPI/Dockerfile
          - name: web
            dockerfile: E-commerce.Web/Dockerfile
          - name: nginx
            dockerfile: nginx/Dockerfile

    steps:
      - name: Checkout code
        uses: actions/checkout@v3

      - name: Login to Azure Container Registry
        uses: docker/login-action@v2
        with:
          registry: ${{ env.ACR_LOGIN_SERVER }}
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}

      - name: Build and push Docker image
        uses: docker/build-push-action@v4
        with:
          context: .
          file: ${{ matrix.service.dockerfile }}
          push: true
          tags: |
            ${{ env.ACR_LOGIN_SERVER }}/${{ matrix.service.name }}:latest
            ${{ env.ACR_LOGIN_SERVER }}/${{ matrix.service.name }}:${{ github.sha }}

  deploy:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/master'

    steps:
      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy to Container Apps
        run: |
          # Update each Container App with new image
          for app in authapi productapi couponapi shoppingcartapi emailapi web nginx-gateway; do
            echo "Updating $app..."
            az containerapp update \
              --name $app \
              --resource-group ${{ env.RESOURCE_GROUP }} \
              --image ${{ env.ACR_LOGIN_SERVER }}/${app}:${{ github.sha }}
          done

      - name: Verify deployment
        run: |
          # Get NGINX URL
          NGINX_URL=$(az containerapp show \
            --name nginx-gateway \
            --resource-group ${{ env.RESOURCE_GROUP }} \
            --query properties.configuration.ingress.fqdn -o tsv)

          # Health check
          echo "Testing health endpoint..."
          curl -f https://$NGINX_URL/health || exit 1

          echo "Deployment successful! URL: https://$NGINX_URL"
```

### 6.3 Create Staging Workflow (Optional)

**Create: `.github/workflows/deploy-staging.yml`**

Similar to production but deploys to separate Container Apps environment for testing.

### 6.4 Add Status Badges to README

**Update README.md:**
```markdown
# E-commerce Microservices

![Build Status](https://github.com/yourusername/E-commerce/workflows/Build%20and%20Deploy/badge.svg)
![Azure](https://img.shields.io/badge/Azure-Container%20Apps-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)

**Live Demo:** https://your-app-url.azurecontainerapps.io
```

**Tasks:**
- [ ] Create service principal for GitHub Actions
- [ ] Add secrets to GitHub repository
- [ ] Create deploy.yml workflow
- [ ] Test workflow on feature branch
- [ ] Merge to master and verify auto-deployment
- [ ] Add status badges to README

**Completion Criteria:**
- ✅ Commits to master trigger automatic deployment
- ✅ All services updated with new images
- ✅ Health checks pass post-deployment
- ✅ Rollback strategy documented

---

## Phase 7: Documentation and Polish (Day 4)

**Goal:** Create professional documentation for portfolio presentation.

### 7.1 Update Main README

**Update: `README.md`**

```markdown
# E-commerce Microservices Platform

A production-ready, cloud-native e-commerce platform built with microservices architecture, demonstrating modern software engineering practices.

## Live Demo

**Application:** https://your-app.azurecontainerapps.io
**API Documentation:** https://your-app.azurecontainerapps.io/swagger

## Architecture

[Insert architecture diagram]

- **6 independent microservices** (Auth, Product, Coupon, ShoppingCart, Email, Web)
- **Event-driven design** with Azure Service Bus
- **API Gateway pattern** with NGINX reverse proxy
- **Container-based deployment** on Azure Container Apps

## Technology Stack

- **Backend:** ASP.NET Core 8.0, Entity Framework Core, JWT Authentication
- **Database:** Azure SQL Database (Serverless)
- **Messaging:** Azure Service Bus
- **Infrastructure:** Docker, Azure Container Apps, Azure Key Vault
- **Monitoring:** Application Insights, Serilog
- **CI/CD:** GitHub Actions

## Key Features

- ✅ Secure authentication with JWT tokens
- ✅ Microservices independence (database-per-service)
- ✅ Asynchronous messaging for email notifications
- ✅ Health checks and graceful degradation
- ✅ Distributed tracing with correlation IDs
- ✅ Resilience patterns (retry, circuit breaker, timeout)
- ✅ Secrets management with Azure Key Vault
- ✅ HTTPS with managed SSL certificates
- ✅ Automated deployments via GitHub Actions

## Local Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for setup instructions.

## Deployment

See [DEPLOYMENT-PLAN.md](DEPLOYMENT-PLAN.md) for production deployment guide.

## Architecture Decisions

See [CLAUDE.md](CLAUDE.md) for detailed codebase documentation.

## Cost Analysis

**Monthly Azure costs:** ~$60
- Container Apps: $30
- SQL Database (Serverless): $5
- Service Bus: $10
- Container Registry: $5
- Key Vault: $1
- Application Insights: $5

## License

MIT License - See [LICENSE](LICENSE) for details.

## Contact

**Author:** Your Name
**Portfolio:** https://yourportfolio.com
**LinkedIn:** https://linkedin.com/in/yourprofile
```

### 7.2 Create Development Guide

**Create: `DEVELOPMENT.md`**

```markdown
# Development Guide

## Prerequisites

- .NET 8 SDK
- SQL Server (or Docker)
- Azure Service Bus namespace
- Docker Desktop (optional)

## Local Setup

### 1. Clone Repository

```bash
git clone https://github.com/yourusername/E-commerce.git
cd E-commerce
```

### 2. Database Setup

**Option A: Local SQL Server**
- Update connection strings in each service's appsettings.Development.json
- Run migrations: `dotnet ef database update` in each API project

**Option B: Docker**
```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name sql-server \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

### 3. Configure Secrets

Create `appsettings.Development.json` in each service:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=E-commerce_[Service];Trusted_Connection=True;TrustServerCertificate=True"
  },
  "ServiceBusConnectionString": "your-servicebus-connection-string",
  "ApiSettings": {
    "JwtOptions": {
      "Secret": "development-secret-key-at-least-32-characters-long",
      "Issuer": "e-commerce-auth-api",
      "Audience": "e-commerce-client"
    }
  }
}
```

### 4. Run Services

**Terminal 1: AuthAPI**
```bash
cd E-commerce.Services.AuthAPI
dotnet run
```

**Terminal 2: ProductAPI**
```bash
cd E-commerce.Services.ProductAPI
dotnet run
```

... repeat for all services

**Or use Docker Compose:**
```bash
docker-compose up
```

### 5. Access Application

- Web UI: https://localhost:7230
- Auth API: https://localhost:7002/swagger
- Product API: https://localhost:7000/swagger
- Coupon API: https://localhost:7001/swagger
- Cart API: https://localhost:7003/swagger

## Testing

### Manual Testing

1. Register a new user via Web UI or AuthAPI
2. Login to receive JWT token
3. Browse products
4. Add items to cart
5. Apply coupon code
6. Checkout (triggers email via Service Bus)

### API Testing with Swagger

1. Navigate to any API's /swagger endpoint
2. Click "Authorize" and enter JWT token (get from login response)
3. Test endpoints

## Debugging

### Visual Studio
- Open E-commerce.sln
- Set multiple startup projects (all 6 services)
- Press F5

### VS Code
- Use compound launch configuration (see .vscode/launch.json)

## Common Issues

**Migration errors:**
```bash
# Delete and recreate database
dotnet ef database drop
dotnet ef database update
```

**Port conflicts:**
- Change ports in launchSettings.json

**Service Bus errors:**
- Verify connection string
- Check queue names match configuration

## Project Structure

See [CLAUDE.md](CLAUDE.md) for detailed architecture documentation.
```

### 7.3 Create Architecture Diagram

**Create: `docs/architecture.md`** (or use Draw.io/Lucidchart)

```markdown
# System Architecture

## High-Level Overview

```
                                    ┌─────────────────┐
                                    │   End Users     │
                                    └────────┬────────┘
                                             │
                                    HTTPS (Port 443)
                                             │
                               ┌─────────────▼──────────────┐
                               │   NGINX API Gateway        │
                               │   (Container App)          │
                               │   - SSL Termination        │
                               │   - Rate Limiting          │
                               │   - Request Routing        │
                               └──┬──────────────────────┬──┘
                                  │                      │
                    /api/product/ │                      │ /
                    /api/coupon/  │                      │
                    /api/auth/    │                      │
                    /api/cart/    │                      │
                 ┌────────────────▼───┐          ┌──────▼──────┐
                 │  Internal APIs     │          │   Web MVC   │
                 │  (Container Apps)  │          │ (Container  │
                 │                    │          │   App)      │
                 │  ┌──────────────┐  │          └─────────────┘
                 │  │  ProductAPI  │  │
                 │  │  (Port 8080) │  │
                 │  └──────┬───────┘  │
                 │         │          │
                 │  ┌──────▼───────┐  │
                 │  │  CouponAPI   │  │
                 │  │  (Port 8080) │  │
                 │  └──────┬───────┘  │
                 │         │          │
                 │  ┌──────▼───────┐  │
                 │  │   AuthAPI    │  │
                 │  │  (Port 8080) │  │────┐
                 │  └──────┬───────┘  │    │ Publishes
                 │         │          │    │ "loguser"
                 │  ┌──────▼───────┐  │    │
                 │  │ ShoppingCart │  │────┼─────┐
                 │  │     API      │  │    │     │ Publishes
                 │  │  (Port 8080) │  │    │     │ "emailshoppingcart"
                 │  └──────────────┘  │    │     │
                 └────────┬────────────┘    │     │
                          │                 │     │
                          ▼                 ▼     ▼
                 ┌─────────────────────────────────────┐
                 │     Azure Service Bus               │
                 │  ┌────────────┐  ┌────────────────┐ │
                 │  │ loguser    │  │ emailshopping  │ │
                 │  │   queue    │  │   cart queue   │ │
                 │  └────────────┘  └────────────────┘ │
                 └────────────┬────────────────────────┘
                              │
                              │ Consumes
                              ▼
                    ┌──────────────────┐
                    │    EmailAPI      │
                    │  (Container App) │
                    │  - Background    │
                    │    Service       │
                    └──────────────────┘
                              │
        ┌─────────────────────┴─────────────────────┐
        │                                           │
        ▼                                           ▼
┌──────────────────┐                    ┌──────────────────┐
│  Azure SQL DB    │                    │ Azure Key Vault  │
│  (5 schemas)     │                    │  - Secrets       │
│  - Auth          │                    │  - Conn Strings  │
│  - Product       │                    │  - JWT Keys      │
│  - Coupon        │                    └──────────────────┘
│  - ShoppingCart  │
│  - Email         │
└──────────────────┘
```

## Service Communication

**Synchronous (HTTP/REST):**
- Web MVC → All APIs (via NGINX)
- ShoppingCartAPI → ProductAPI (fetch product details)
- ShoppingCartAPI → CouponAPI (validate coupons)

**Asynchronous (Azure Service Bus):**
- AuthAPI → EmailAPI (user registration events)
- ShoppingCartAPI → EmailAPI (cart summary emails)

## Security

- All external traffic over HTTPS (managed certificate)
- JWT token authentication (256-bit secret in Key Vault)
- Managed identities for Azure resource access
- Secrets stored in Azure Key Vault (no hardcoded credentials)
- API rate limiting via NGINX (100 req/s general, 10 req/s auth)

## Scalability

- Container Apps auto-scale based on HTTP requests
- Database uses serverless tier (auto-pause when idle)
- Service Bus queue for async processing (decouples services)
- Stateless services (can scale horizontally)

## Observability

- Application Insights (distributed tracing)
- Serilog structured logging (correlation IDs)
- Health check endpoints (/health, /health/ready)
- NGINX access logs with timing metrics
```

### 7.4 Create Cost Breakdown Document

**Create: `docs/COSTS.md`**

```markdown
# Azure Infrastructure Costs

## Monthly Cost Breakdown (Estimated)

| Service | Tier/SKU | Quantity | Monthly Cost |
|---------|----------|----------|--------------|
| **Azure Container Apps** | Consumption | 6 apps × $0.000012/vCPU-second | ~$30 |
| **Azure SQL Database** | Serverless (Gen5, 1 vCore) | 1 database | $5 |
| **Azure Service Bus** | Basic | 1 namespace | $10 |
| **Azure Container Registry** | Basic | 1 registry | $5 |
| **Azure Key Vault** | Standard | 1 vault | $0.03 × operations | ~$1 |
| **Application Insights** | Pay-as-you-go | ~1GB data/month | ~$5 |
| **Outbound Bandwidth** | First 100GB free | <10GB expected | $0 |
| **SSL Certificate** | Managed (Container Apps) | Free | $0 |
| **Total** | | | **~$56/month** |

## Cost Optimization Strategies

### Implemented
- ✅ SQL Database Serverless (auto-pause after 1 hour idle)
- ✅ Single database with multiple schemas (vs 5 separate DBs)
- ✅ Container Apps on Consumption plan (pay-per-use)
- ✅ Basic tier services (no premium features needed)
- ✅ Managed SSL certificates (free vs $70/year)

### Future Optimizations
- Scale to zero for non-critical services (EmailAPI during off-hours)
- Use Azure Front Door instead of NGINX for global distribution (if needed)
- Implement caching to reduce database queries
- Archive old logs to reduce Application Insights costs

## Alternative Low-Cost Setup (~$30/month)

| Change | Monthly Savings |
|--------|-----------------|
| Use Azure Container Instances instead of Container Apps | -$15 |
| Use Azure SQL Basic tier (always on) instead of Serverless | -$0 (similar cost) |
| Remove Application Insights, use free Log Analytics | -$5 |
| **Total** | **~$31/month** |

**Trade-offs:**
- No auto-scaling
- Manual container orchestration
- Less integrated monitoring

## Production-Scale Estimate (~$200-300/month)

For 10,000 daily active users:
- Premium Container Apps (dedicated compute)
- SQL Database Standard tier (S2)
- Service Bus Standard tier (topics + subscriptions)
- Azure Front Door for CDN
- Increased Application Insights data
- Redis Cache for session management

## ROI for Portfolio

**Cost:** $56/month = $672/year
**Value:** Demonstrates $100K+ worth of enterprise skills
**Return:** Priceless for job search (one interview covers 12 months of hosting)
```

### 7.5 Create Final Checklist

**Add to this document:**

## Final Pre-Launch Checklist

### Security
- [ ] All secrets removed from source control
- [ ] Key Vault access policies configured
- [ ] Managed identities enabled for all services
- [ ] HTTPS enforced (no HTTP allowed)
- [ ] JWT secret is cryptographically secure (256-bit)
- [ ] SQL firewall rules restrict access
- [ ] Service Bus uses least-privilege access policies
- [ ] Security headers present (X-Frame-Options, CSP, etc.)
- [ ] Rate limiting enabled on APIs
- [ ] No default passwords in seed data

### Functionality
- [ ] User registration works
- [ ] Login returns valid JWT token
- [ ] Products display correctly
- [ ] Coupons apply discounts
- [ ] Shopping cart persists items
- [ ] Checkout triggers email notification
- [ ] Service Bus messages flow correctly
- [ ] Database migrations applied successfully

### Observability
- [ ] Health check endpoints respond
- [ ] Application Insights receiving telemetry
- [ ] Logs contain correlation IDs
- [ ] Distributed traces visible in App Insights
- [ ] NGINX access logs working

### Performance
- [ ] APIs respond in <500ms (p95)
- [ ] Database queries optimized (no N+1 queries)
- [ ] Retry policies prevent cascading failures
- [ ] Circuit breakers trip under load

### Documentation
- [ ] README.md updated with live URL
- [ ] Architecture diagram included
- [ ] API endpoints documented (Swagger)
- [ ] Environment variables documented
- [ ] Deployment process documented
- [ ] Cost breakdown provided

### Portfolio Presentation
- [ ] Live demo accessible (no auth required for viewing)
- [ ] GitHub repo is public
- [ ] Code is clean and well-commented
- [ ] Commit history shows incremental progress
- [ ] README highlights key technical decisions
- [ ] Architecture emphasizes scalability

---

## Post-Deployment Tasks

### Week 1
- [ ] Monitor Application Insights for errors
- [ ] Check cost management dashboard
- [ ] Verify auto-scaling works under load
- [ ] Test disaster recovery (delete one Container App, redeploy)

### Week 2
- [ ] Load test with Apache Bench or k6
- [ ] Review security with Azure Security Center
- [ ] Optimize slow queries identified in App Insights
- [ ] Set up alerts for downtime/errors

### Ongoing
- [ ] Update README with lessons learned
- [ ] Blog post about deployment experience
- [ ] LinkedIn post showcasing architecture
- [ ] Add to portfolio website with screenshot

---

## Troubleshooting Guide

### Container App won't start
```bash
# Check logs
az containerapp logs show --name productapi --resource-group ecommerce-rg --follow

# Common issues:
# - Missing environment variable
# - Key Vault access denied (check managed identity)
# - Database connection string incorrect
# - Port mismatch (should be 8080)
```

### Service Bus messages not processing
```bash
# Check EmailAPI logs
az containerapp logs show --name emailapi --resource-group ecommerce-rg

# Verify queue exists
az servicebus queue show --namespace-name ecommerceweb --name loguser

# Check dead-letter queue
az servicebus queue show --namespace-name ecommerceweb --name loguser --query countDetails
```

### SQL Database timeout errors
```bash
# Check if database is paused (Serverless tier)
az sql db show --name ecommerce-db --server ecommerce-sql-server --resource-group ecommerce-rg --query status

# Increase timeout in connection string
Server=...;Connection Timeout=60;
```

### High costs
```bash
# Check cost breakdown
az consumption usage list --start-date 2025-11-01 --end-date 2025-11-09

# Identify expensive resources
az cost-management export list --scope /subscriptions/{subscription-id}
```

---

## Success Metrics

**Deployment is successful when:**
- ✅ Application accessible via HTTPS
- ✅ No errors in Application Insights (last 24 hours)
- ✅ All health checks green
- ✅ Service Bus processing messages
- ✅ Database queries <100ms average
- ✅ Total monthly cost under $70
- ✅ 99.9% uptime (measured over 30 days)

**Portfolio is ready when:**
- ✅ Live demo link works
- ✅ GitHub README professionally formatted
- ✅ Can explain architecture in 2-minute elevator pitch
- ✅ Can answer "Why microservices?" with confidence
- ✅ Can discuss trade-offs (cost vs scalability)
- ✅ Can demo end-to-end flow in interview

---

## Next Steps After Deployment

### Phase 8: Enhancements (Optional)

**If you want to go beyond minimal deployment:**

1. **Add Redis Cache** (~$15/month)
   - Cache product catalog
   - Cache JWT validation
   - Session state for Web MVC

2. **Implement Ocelot API Gateway** (replaces NGINX)
   - More sophisticated routing
   - Built-in ASP.NET Core middleware
   - Better integration with .NET ecosystem

3. **Add Monitoring Dashboard**
   - Grafana for metrics visualization
   - Custom dashboard in Azure Portal
   - Real-time alerting via email/SMS

4. **Implement Actual Email Sending**
   - SendGrid integration (~$15/month for 40k emails)
   - Email templates with Razor
   - Unsubscribe functionality

5. **Add Payment Processing**
   - Stripe integration
   - PaymentAPI microservice
   - Idempotent checkout

6. **Implement Order Management**
   - OrderAPI microservice
   - Order history
   - Shipping integration

---

## Lessons Learned (Fill in after deployment)

**What went well:**
-

**What was challenging:**
-

**What I would do differently:**
-

**Key takeaways for interviews:**
-

---

**Deployment Status:** ✅ Phase 3-Lite Complete - Ready for Phase 4 (Azure Deployment)

**Current Phase:** Phase 4 - Azure Infrastructure Setup

**Completion:** [✅] Phase 1 | [✅] Phase 2 | [✅] Phase 3-Lite | [⏳] Phase 4 | [ ] Phase 5 | [ ] Phase 6 | [ ] Phase 7

**Live URL:** [Update when deployed]

**Total Time Investment:**
- Phase 1: ~45 minutes (Security hardening)
- Phase 2: ~1.5 hours (Containerization)
- Phase 3-Lite: ~15 minutes (Basic health checks)
- **Total so far:** ~2.5 hours
- **Phase 4 estimate:** 1-2 hours (first deployment)

**Projected Monthly Cost:** ~$60-70 (5 databases × $5 + Container Apps + Service Bus + ACR)

---

## Phase 4: Final Deployment Decisions (2025-11-15)

### Critical Design Choices

| Decision Point | Final Choice | Rationale |
|----------------|--------------|-----------|
| **Database Strategy** | ✅ 5 Separate Databases ($25/month) | Matches local dev, zero code changes, true microservices isolation |
| **Service Discovery** | ✅ Azure Container Apps DNS | Use short names: `http://productapi` (auto-resolved in same environment) |
| **Auto-Migration** | ✅ Disabled in Production | Manual migrations before deployment to prevent race conditions |
| **Secrets Management** | ✅ Environment Variables Initially | Use env vars for MVP, migrate to Key Vault post-deployment (optional) |
| **Service Bus Queues** | ✅ Pre-Create Infrastructure | Create `loguser` and `emailshoppingcart` queues before deploying services |
| **Application Insights** | ⏳ Skip Initially | Add post-deployment when observability/tracing needed |
| **NGINX Gateway** | ⏳ Skip Initially | Azure Container Apps provides built-in ingress, add Phase 5 if needed |
| **CORS Configuration** | ✅ Required for Production | Add `AllowAll` policy for cross-origin API calls from Web MVC |

### Why 5 Separate Databases?

**Rejected: Single Database + Schema Separation**
- ❌ Would break local dev setup (currently uses 5 separate databases)
- ❌ Requires code changes in all ApplicationDbContext files
- ❌ Requires regenerating all EF Core migrations
- ❌ Violates microservices principle (database-per-service)
- ❌ Saves only $20/month, costs hours of refactoring

**Chosen: 5 Separate Databases (Serverless)**
- ✅ Zero code changes - deploy as-is
- ✅ Matches local development environment exactly
- ✅ True microservices isolation
- ✅ Can scale/backup each database independently
- ✅ Easier to debug and maintain
- ⚠️ Costs $20 more per month ($25 vs $5)

**Cost-Benefit Analysis:** Spending $20/month to avoid 3-4 hours of refactoring work is the right trade-off for MVP.

**Final Monthly Cost:** ~$70/month (updated from initial estimate)

---

## Resources

**Project Documentation:**
- [PUSH-TO-PRODUCTION.md](PUSH-TO-PRODUCTION.md) - **Quick deployment guide for Phase 4** ⭐
- [PHASE4-PROGRESS.md](PHASE4-PROGRESS.md) - **Progress tracker with timestamps for Phase 4** ⭐
- [AZURE-ENV-VARS.md](../AZURE-ENV-VARS.md) - Complete environment variables reference for Azure deployment
- [PHASE2.md](PHASE2.md) - Containerization guide (MVP and Full approaches)
- [PHASE2-STEPS.md](PHASE2-STEPS.md) - Step-by-step progress tracker for Phase 2
- [CLAUDE.md](../CLAUDE.md) - Codebase architecture documentation

**Automation Scripts:**
- [scripts/disable-auto-migration.ps1](../scripts/disable-auto-migration.ps1) - Disable auto-migration for production (Windows)
- [scripts/disable-auto-migration.sh](../scripts/disable-auto-migration.sh) - Disable auto-migration for production (Linux/Mac)
- [scripts/setup-user-secrets.ps1](../scripts/setup-user-secrets.ps1) - Local development secrets setup
- [scripts/rebuild-docker-images.ps1](../scripts/rebuild-docker-images.ps1) - Rebuild all Docker images
- [scripts/test-health-endpoints.ps1](../scripts/test-health-endpoints.ps1) - Test health endpoints

**Azure Documentation:**
- [Container Apps](https://learn.microsoft.com/azure/container-apps/)
- [Azure SQL Database Serverless](https://learn.microsoft.com/azure/azure-sql/database/serverless-tier-overview)
- [Azure Service Bus](https://learn.microsoft.com/azure/service-bus-messaging/)
- [Azure Key Vault](https://learn.microsoft.com/azure/key-vault/)

**Tools:**
- [Azure Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/)
- [Docker Documentation](https://docs.docker.com/)
- [NGINX Configuration Guide](https://nginx.org/en/docs/)

**Community:**
- [ASP.NET Core GitHub](https://github.com/dotnet/aspnetcore)
- [Microservices.io](https://microservices.io/)
- [12 Factor App](https://12factor.net/)
