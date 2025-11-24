# Scripts Directory

This directory contains utility scripts for development, testing, and deployment.

---

## Available Scripts

### 1. Run Database Migrations (Phase 4.3)
**File:** `run-migrations.ps1`

**Purpose:** Apply EF Core migrations to all 5 Azure SQL databases

**Usage:**
```powershell
.\scripts\run-migrations.ps1
```

**What it does:**
- Loads connection strings from `.env` file
- Validates all connection strings are present
- Runs migrations sequentially for all 5 services:
  - AuthAPI (ecommerce-auth)
  - ProductAPI (ecommerce-product)
  - CouponAPI (ecommerce-coupon)
  - ShoppingCartAPI (ecommerce-cart)
  - EmailAPI (ecommerce-email)
- Creates database schema and applies seed data
- Provides detailed progress report with timing

**When to use:**
- **Phase 4.3:** Before first Azure deployment (MUST RUN ONCE)
- **Future updates:** When adding new migrations to production

**Prerequisites:**
- .NET 8.0 SDK installed
- `.env` file configured with Azure SQL connection strings
- Azure SQL databases created
- Firewall allows your IP address

**Output:**
- Creates `__EFMigrationsHistory` table in each database
- Seeds data: 4 products, 2 coupons, roles
- Idempotent: Safe to run multiple times

---

### 2. Setup User Secrets (Phase 1)
**File:** `setup-user-secrets.ps1`

**Purpose:** Configure User Secrets for all 5 microservices

**Usage:**
```powershell
.\scripts\setup-user-secrets.ps1
```

**What it does:**
- Prompts for Azure Service Bus connection string
- Prompts for JWT secret (or generates one)
- Configures User Secrets for all services
- Validates configuration

**When to use:** After Phase 1 (Security Hardening)

---

### 3. Health Endpoint Tester (Phase 3)
**File:** `test-health-endpoints.ps1`

**Purpose:** Test all 6 service health endpoints

**Usage:**
```powershell
.\scripts\test-health-endpoints.ps1
```

**What it does:**
- Tests `/health` endpoint for all 6 services
- Provides summary report
- Exits with code 0 (success) or 1 (failure)

**When to use:**
- After starting services locally
- After rebuilding Docker images
- Before Phase 4 (Azure deployment)

**Prerequisites:**
- Services must be running (locally or in Docker)
- Ports 7000-7003, 7230, 7298 must be accessible

---

### 4. Docker Image Rebuild (Phase 2 & 3)
**File:** `rebuild-docker-images.ps1`

**Purpose:** Rebuild all Docker images with latest code changes

**Usage:**
```powershell
.\scripts\rebuild-docker-images.ps1
```

**What it does:**
- Checks if Docker is running
- Optionally removes old images
- Builds all 6 service images using docker-compose
- Shows build time and next steps

**When to use:**
- After modifying any service code
- After adding health check endpoints (Phase 3)
- Before testing in Docker containers
- Before pushing to Azure Container Registry

**Prerequisites:**
- Docker Desktop running
- `.env` file configured (for environment variables)

---

## Script Execution Order

### Phase 4: Azure Deployment (ONE TIME)
```powershell
# 1. Create Azure infrastructure (via Portal or Azure CLI)
#    - Resource Group, SQL Server, Databases, Service Bus, ACR

# 2. Run migrations (CRITICAL - DO THIS ONCE BEFORE DOCKER BUILD)
.\scripts\run-migrations.ps1
#    Creates schemas and seed data in Azure SQL

# 3. Build Docker images
.\scripts\rebuild-docker-images.ps1

# 4. Push to ACR (via Docker CLI)
#    docker tag ... ecommerceacr.azurecr.io/...
#    docker push ...

# 5. Deploy to Container Apps (via Portal or Bicep)
```

### First-Time Setup (Local Development)
```powershell
# 1. Phase 1: Configure secrets
.\scripts\setup-user-secrets.ps1

# 2. Phase 2: Build Docker images
.\scripts\rebuild-docker-images.ps1

# 3. Phase 3: Test health checks
.\scripts\test-health-endpoints.ps1
```

### Development Workflow
```powershell
# 1. Make code changes

# 2. Rebuild affected images
.\scripts\rebuild-docker-images.ps1

# 3. Start containers
docker-compose up -d

# 4. Test health endpoints
.\scripts\test-health-endpoints.ps1

# 5. Stop containers when done
docker-compose down
```

### Future Production Updates
```powershell
# When adding new migrations:
# 1. Create migration in code: dotnet ef migrations add FeatureName
# 2. Run migrations against Azure SQL: .\scripts\run-migrations.ps1
# 3. Rebuild Docker images: .\scripts\rebuild-docker-images.ps1
# 4. Push to ACR and redeploy
```

---

## Common Issues

### "Execution policy" error

**Error:**
```
.\scripts\test-health-endpoints.ps1 : File cannot be loaded because running scripts is disabled on this system.
```

**Solution:**
```powershell
# Run once (as Administrator)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Docker not running

**Error:**
```
✗ Docker is not running. Please start Docker Desktop.
```

**Solution:**
1. Start Docker Desktop
2. Wait for Docker to fully start
3. Run script again

### Services not responding

**Error:**
```
Testing ProductAPI... ✗ FAILED (not responding)
```

**Solution:**
```bash
# Check if service is running
docker-compose ps

# Check logs
docker-compose logs productapi

# Restart service
docker-compose restart productapi
```

---

## Script Output Examples

### Successful Health Check Test
```
========================================
E-commerce Health Check Endpoint Tester
========================================

Testing health endpoints...

Testing ProductAPI... ✓ HEALTHY
Testing CouponAPI... ✓ HEALTHY
Testing AuthAPI... ✓ HEALTHY
Testing ShoppingCartAPI... ✓ HEALTHY
Testing EmailAPI... ✓ HEALTHY
Testing Web MVC... ✓ HEALTHY

========================================
Summary
========================================

ProductAPI (Port 7000): ✓ Healthy
CouponAPI (Port 7001): ✓ Healthy
AuthAPI (Port 7002): ✓ Healthy
ShoppingCartAPI (Port 7003): ✓ Healthy
EmailAPI (Port 7298): ✓ Healthy
Web MVC (Port 7230): ✓ Healthy

========================================
✓ All services are healthy!
Ready for Phase 4 (Azure Deployment)
```

### Successful Docker Rebuild
```
========================================
Docker Image Rebuild Script
========================================

Checking Docker...
✓ Docker is running

Working directory: C:\Users\minha\source\repos\E-commerce

Building Docker images...
This will take 5-10 minutes on first build...

[+] Building 234.5s (78/78) FINISHED
...

========================================
✓ All images built successfully!
Build time: 3 minutes 54 seconds
========================================

Built images:
e-commerce-authapi               latest    2 minutes ago
e-commerce-productapi            latest    2 minutes ago
e-commerce-couponapi             latest    2 minutes ago
e-commerce-shoppingcartapi       latest    2 minutes ago
e-commerce-emailapi              latest    2 minutes ago
e-commerce-web                   latest    2 minutes ago

Next steps:
1. Start services: docker-compose up -d
2. Check status: docker-compose ps
3. Test health checks: .\scripts\test-health-endpoints.ps1
```

---

## Related Documentation

- [DEPLOYMENT-PLAN.md](../Docs/DEPLOYMENT-PLAN.md) - Overall deployment strategy
- [PHASE2.md](../Docs/PHASE2.md) - Containerization guide
- [PHASE3-TESTING-GUIDE.md](../Docs/PHASE3-TESTING-GUIDE.md) - Detailed testing instructions
- [AZURE-ENV-VARS.md](../AZURE-ENV-VARS.md) - Environment variables for Azure

---

**Last Updated:** 2025-11-17
**Updated by:** Phase 4.3-4.4 Azure Deployment
**Last changes:** Added run-migrations.ps1 documentation, updated execution order for Azure deployment
