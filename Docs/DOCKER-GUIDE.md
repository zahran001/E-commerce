# Docker Deployment Guide

## Build All Images

From the repository root:

```bash
docker build -t ecommerce/authapi:latest -f E-commerce.Services.AuthAPI/Dockerfile .
docker build -t ecommerce/productapi:latest -f E-commerce.Services.ProductAPI/Dockerfile .
docker build -t ecommerce/couponapi:latest -f E-commerce.Services.CouponAPI/Dockerfile .
docker build -t ecommerce/shoppingcartapi:latest -f E-commerce.Services.ShoppingCartAPI/Dockerfile .
docker build -t ecommerce/emailapi:latest -f Ecommerce.Services.EmailAPI/Dockerfile .
docker build -t ecommerce/web:latest -f E-commerce.Web/Dockerfile .
```

Image sizes are ~220-250 MB each (optimized via multi-stage builds).

---

## Docker Compose (Local Testing)

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

### Services Defined

All 6 microservices with proper dependency ordering:

| Service | Host Port | Container Port |
|---------|-----------|----------------|
| AuthAPI | 7002 | 8080 |
| ProductAPI | 7000 | 8080 |
| CouponAPI | 7001 | 8080 |
| ShoppingCartAPI | 7003 | 8080 |
| EmailAPI | 7298 | 8080 |
| Web MVC | 7230 | 8080 |

### Example Service Definition

```yaml
authapi:
  image: ecommerce/authapi:latest
  build:
    context: .
    dockerfile: E-commerce.Services.AuthAPI/Dockerfile
  ports:
    - "7002:8080"
  environment:
    - ConnectionStrings__DefaultConnection=Server=host.docker.internal;Database=E-commerce_Auth;Trusted_Connection=True;TrustServerCertificate=True
    - ServiceBusConnectionString=${ServiceBusConnectionString}
    - ApiSettings__Secret=${JWT_SECRET}
    - Jaeger__AgentHost=host.docker.internal
    - Jaeger__AgentPort=6831
  depends_on:
    - productapi
    - couponapi
```

### SQL Server Connection

- Uses `host.docker.internal` to connect to SQL Server running on the host machine
- Alternative: Use a SQL Server container (requires volume mounts for persistence)

### Environment Variables

| Variable | Example | Notes |
|----------|---------|-------|
| `ServiceUrls__ProductAPI` | `http://productapi` | Internal DNS within docker-compose |
| `ConnectionStrings__DefaultConnection` | `Server=host.docker.internal;...` | Points to host SQL Server |
| `ServiceBusConnectionString` | `Endpoint=sb://...` | From Azure or User Secrets |
| `ApiSettings__Secret` | 32+ character string | JWT signing key |
| `Jaeger__AgentHost` | `host.docker.internal` | Jaeger running on host |

---

## Push to Azure Container Registry

See [PUSH-TO-PRODUCTION.md](PUSH-TO-PRODUCTION.md) for complete Docker build and ACR push instructions.

Also see the automation scripts:
- [scripts/Prod/build-docker-images.ps1](../scripts/Prod/build-docker-images.ps1)
- [scripts/Prod/push-docker-images.ps1](../scripts/Prod/push-docker-images.ps1)
