# Azure Deployment

## Quick Deployment Guide

Full step-by-step instructions: [PUSH-TO-PRODUCTION.md](PUSH-TO-PRODUCTION.md)

**Summary:**
1. **Infrastructure Setup** (20 min) - Create Azure resources (SQL Server, Service Bus, Container Registry, Container Apps Environment)
2. **Database Migrations** (10 min) - Apply EF Core migrations to Azure SQL databases
3. **Build & Push Images** (20 min) - Build Docker images and push to Azure Container Registry
4. **Deploy Services** (30 min) - Create Container Apps with environment variables and health probes
5. **Verification** (10 min) - Test endpoints, verify Service Bus message flow

**Total first-deployment time:** ~2 hours

---

## Live Deployment Details

**Azure Container Apps Environment:**
- **Environment Name:** `ecommerce-env`
- **Region:** East US
- **Default Domain:** `mangosea-a7508352.eastus.azurecontainerapps.io`

### Deployed Services

| Service | Status | FQDN | Ingress |
|---------|--------|------|---------|
| **Web MVC** | Running | `web.mangosea-a7508352.eastus.azurecontainerapps.io` | External (Public) |
| **ProductAPI** | Running | `productapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io` | Internal Only |
| **CouponAPI** | Running | `couponapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io` | Internal Only |
| **AuthAPI** | Running | `authapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io` | Internal Only |
| **ShoppingCartAPI** | Running | `shoppingcartapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io` | Internal Only |
| **EmailAPI** | Running | `emailapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io` | Internal Only |

### Infrastructure Resources

- **SQL Server:** `ecommerce-sql-server-prod.database.windows.net`
- **Service Bus Namespace:** `ecommerceweb.servicebus.windows.net`
- **Container Registry:** `ecommerceacr.azurecr.io`
- **Resource Group:** `Ecommerce-Project`

---

## Container Apps Resource Allocation

```yaml
ProductAPI, CouponAPI, AuthAPI, ShoppingCartAPI:
  cpu: 0.5 vCPU
  memory: 1 GiB
  replicas: min=1, max=1
  ingress: internal (within environment only)
  port: 8080

EmailAPI:
  cpu: 0.25 vCPU
  memory: 0.5 GiB
  replicas: min=1, max=1
  ingress: internal
  port: 8080

Web MVC:
  cpu: 0.5 vCPU
  memory: 1 GiB
  replicas: min=1, max=2 (auto-scale on HTTP traffic)
  ingress: external (public internet)
  port: 8080
```

---

## Environment Variables Strategy

```bash
# Service URLs (override appsettings.json at runtime)
ServiceUrls__ProductAPI=http://productapi
ServiceUrls__CouponAPI=http://couponapi
ServiceUrls__AuthAPI=http://authapi
ServiceUrls__ShoppingCartAPI=http://shoppingcartapi

# Database connections
ConnectionStrings__DefaultConnection=Server=tcp:...;Database=...

# Service Bus
ServiceBusConnectionString=Endpoint=sb://...

# JWT Configuration
ApiSettings__Secret=<256-bit-secret>
ApiSettings__Issuer=e-commerce-auth-api
ApiSettings__Audience=e-commerce-client
```

---

## Production Architecture

```
                                    Internet
                                       |
                          [Azure Container Apps - East US]
                                       |
                    +--------------------------------------+
                    |  Web MVC (External Ingress)          |
                    |  HTTPS with managed SSL              |
                    +------------------+-------------------+
                                       |
                    Internal Service Mesh (DNS-based)
                                       |
        +--------------+-----------+----------+--------------+
        |              |           |          |              |
   +--------+    +--------+  +--------+  +----------+  +--------+
   | AuthAPI|    |Product |  | Coupon |  |Shopping  |  | Email  |
   | :8080  |    |API:8080|  |API:8080|  |CartAPI   |  |API:8080|
   +----+---+    +----+---+  +----+---+  |:8080     |  +----+---+
        |             |           |      +-----+----+       |
        v             v           v            v            v
   +------------------------------------------------------------+
   |           Azure SQL Server (Serverless)                    |
   |  ecommerce-sql-server-prod.database.windows.net            |
   |  5 databases: Auth, Product, Coupon, Cart, Email           |
   +------------------------------------------------------------+

   +-----------------------------------------------------+
   |                OBSERVABILITY STACK                   |
   |  Redis (Caching) | Seq (Logging) | Jaeger (Tracing) |
   |                       |                              |
   |              [Azure Files: 32GB]                     |
   +-----------------------------------------------------+

        +------------------------------------+
        |         Azure Service Bus          |
        |  ecommerceweb.servicebus.net       |
        |  Queue: loguser       (Auth->Email)|
        |  Queue: emailshoppingcart (Cart->Email)|
        +------------------------------------+

   +-----------------------------------------+
   |        Azure Container Registry         |
   |  ecommerceacr.azurecr.io                |
   |  6 images: authapi, productapi,         |
   |  couponapi, shoppingcartapi,            |
   |  emailapi, web (all :1.0.1)             |
   +-----------------------------------------+
```

---

## Deployment Metrics

- **Docker Images:** 6 services, 220-250 MB each (multi-stage builds)
- **Container Apps:** 6 running containers with health probes
- **Databases:** 5 Azure SQL Serverless databases (auto-pause after 1 hour idle)
- **Message Queues:** 2 Service Bus queues
- **Environment Variables:** 20+ configuration overrides per service
- **Deployment Strategy:** Manual via Azure CLI (CI/CD planned), rolling updates with zero downtime

---

## Deployment Progress Tracking

- [PHASE4-PROGRESS.md](PHASE4-PROGRESS.md) - Core infrastructure deployment
- [Deploy-ObservabilityStack/PHASE5-DEPLOYMENT.md](Deploy-ObservabilityStack/PHASE5-DEPLOYMENT.md) - Observability stack (Redis, Seq, Jaeger)

## Automation Scripts

- [scripts/Prod/build-docker-images.ps1](../scripts/Prod/build-docker-images.ps1) - Build all Docker images
- [scripts/Prod/push-docker-images.ps1](../scripts/Prod/push-docker-images.ps1) - Push images to ACR
- [scripts/Prod/deploy-all-services.ps1](../scripts/Prod/deploy-all-services.ps1) - Deploy to Container Apps
- [scripts/Prod/Post-deployment/health-check.ps1](../scripts/Prod/Post-deployment/health-check.ps1) - Validate health endpoints
