# Phase 3-Lite Testing Guide

**Purpose:** Validate health check endpoints before Phase 4 (Azure deployment)

**Time Required:** 10-15 minutes

---

## Prerequisites

- ✅ Phase 1 completed (secrets externalized)
- ✅ Phase 2 completed (Dockerfiles created)
- ✅ Phase 3-Lite completed (health endpoints added)
- ✅ Services running locally OR Docker images rebuilt

---

## Option 1: Test Running Services (Quick - 2 minutes)

If your services are already running locally (via Visual Studio or `dotnet run`):

### Using PowerShell Script (Recommended)

```powershell
# From repository root
.\scripts\test-health-endpoints.ps1
```

**Expected Output:**
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

### Manual Browser Testing

Open these URLs in your browser (one at a time):

1. **ProductAPI:** https://localhost:7000/health
2. **CouponAPI:** https://localhost:7001/health
3. **AuthAPI:** https://localhost:7002/health
4. **ShoppingCartAPI:** https://localhost:7003/health
5. **EmailAPI:** https://localhost:7298/health
6. **Web MVC:** https://localhost:7230/health

**Expected Response (each):**
```json
{
  "status": "healthy",
  "service": "ServiceName",
  "timestamp": "2025-11-12T14:30:00.123Z"
}
```

### Using curl (Command Line)

```bash
# Test all services
curl https://localhost:7000/health -k
curl https://localhost:7001/health -k
curl https://localhost:7002/health -k
curl https://localhost:7003/health -k
curl https://localhost:7298/health -k
curl https://localhost:7230/health -k
```

**Note:** `-k` flag ignores SSL certificate validation (safe for local development)

---

## Option 2: Test in Docker Containers (Thorough - 15 minutes)

This validates health checks work in the containerized environment (closest to Azure).

### Step 1: Rebuild Docker Images

```powershell
# From repository root
.\scripts\rebuild-docker-images.ps1
```

**What this does:**
- Checks if Docker is running
- Optionally removes old images
- Builds all 6 service images with new health check code
- Shows build summary and next steps

**Expected build time:** 5-10 minutes (first build), 2-3 minutes (subsequent builds)

### Step 2: Start Containers

```bash
# Start all services in detached mode (background)
docker-compose up -d

# Expected output:
# Creating network "e-commerce_default" with the default driver
# Creating ecommerce-authapi ... done
# Creating ecommerce-productapi ... done
# Creating ecommerce-couponapi ... done
# Creating ecommerce-shoppingcartapi ... done
# Creating ecommerce-emailapi ... done
# Creating ecommerce-web ... done
```

### Step 3: Wait for Startup

```bash
# Watch container status (wait for "healthy" status)
docker-compose ps

# Expected output (wait until all show "Up"):
# NAME                         STATUS              PORTS
# ecommerce-authapi            Up                  0.0.0.0:7002->8080/tcp
# ecommerce-productapi         Up                  0.0.0.0:7000->8080/tcp
# ecommerce-couponapi          Up                  0.0.0.0:7001->8080/tcp
# ecommerce-shoppingcartapi    Up                  0.0.0.0:7003->8080/tcp
# ecommerce-emailapi           Up                  0.0.0.0:7298->8080/tcp
# ecommerce-web                Up                  0.0.0.0:7230->8080/tcp
```

**Note:** May take 30-60 seconds for all containers to start.

### Step 4: Test Health Endpoints

```powershell
# Run health check script
.\scripts\test-health-endpoints.ps1
```

**Or manually test:**
```bash
# Test each service
curl http://localhost:7000/health
curl http://localhost:7001/health
curl http://localhost:7002/health
curl http://localhost:7003/health
curl http://localhost:7298/health
curl http://localhost:7230/health
```

**Note:** Use `http://` not `https://` when testing Docker containers (HTTPS termination will be handled by Azure)

### Step 5: Check Container Logs (If Issues)

```bash
# View logs for all services
docker-compose logs -f

# Or view logs for specific service
docker-compose logs -f productapi
docker-compose logs -f authapi
```

**Common issues:**
- Database connection errors: Check if SQL Server is running and connection string is correct in `.env`
- Service Bus errors: Check if `AZURE_SERVICEBUS_CONNECTION` is set in `.env`
- Port conflicts: Check if ports 7000-7003, 7230, 7298 are available

### Step 6: Stop Containers

```bash
# Stop all services (preserves volumes)
docker-compose down

# Or stop and remove volumes (clean slate)
docker-compose down -v
```

---

## Troubleshooting

### Issue: "Docker is not running"

**Solution:**
1. Start Docker Desktop
2. Wait for Docker to fully start (icon turns green)
3. Run script again

### Issue: Health endpoint returns 404

**Possible causes:**
1. Service not fully started yet (wait 30 seconds)
2. Health endpoint not added to Program.cs (check code)
3. Wrong port number (check docker-compose.yml)

**Solution:**
```bash
# Check if service is listening
docker-compose logs servicename

# Restart specific service
docker-compose restart servicename
```

### Issue: Connection refused

**Possible causes:**
1. Service crashed on startup
2. Database migration failed
3. Missing environment variable

**Solution:**
```bash
# Check logs
docker-compose logs servicename

# Common fixes:
# 1. Check .env file exists and has correct values
# 2. Verify SQL Server is running (for host.docker.internal)
# 3. Check for missing secrets in .env
```

### Issue: Build fails with "Access Denied"

**Solution:**
```bash
# Stop all containers first
docker-compose down

# Clean up old containers/images
docker system prune -a

# Rebuild
.\scripts\rebuild-docker-images.ps1
```

---

## Success Criteria

Before proceeding to Phase 4, ensure:

- ✅ All 6 health endpoints return HTTP 200
- ✅ Response contains `"status": "healthy"`
- ✅ Each service identifies itself correctly (`"service": "ServiceName"`)
- ✅ Timestamp is recent (within last minute)
- ✅ No errors in docker-compose logs

---

## Quick Commands Reference

```bash
# Build images
docker-compose build

# Start services (detached)
docker-compose up -d

# Check status
docker-compose ps

# View logs (all services)
docker-compose logs -f

# View logs (specific service)
docker-compose logs -f productapi

# Stop services
docker-compose down

# Stop and remove volumes
docker-compose down -v

# Restart specific service
docker-compose restart productapi

# Test health endpoint
curl http://localhost:7000/health
```

---

## What's Next?

Once all health checks pass:

1. ✅ Commit your changes to Git
   ```bash
   git add .
   git commit -m "Phase 3-Lite: Add health check endpoints

   - Added /health endpoint to all 6 services
   - Returns status, service name, and timestamp
   - Ready for Azure Container Apps health probes

   🤖 Generated with Claude Code"
   ```

2. ✅ Move to Phase 4: Azure Infrastructure Setup
   - See [DEPLOYMENT-PLAN.md](DEPLOYMENT-PLAN.md#phase-4-azure-infrastructure-setup-day-3)

3. ✅ Azure will use these health endpoints for:
   - Liveness probes (is container running?)
   - Readiness probes (is container ready for traffic?)
   - Auto-restart unhealthy containers

---

**Last Updated:** 2025-11-12
**Status:** Ready for Phase 4 Testing
**Next Phase:** Azure Infrastructure Setup
