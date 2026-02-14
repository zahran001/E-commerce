# E-commerce Microservices Platform

A cloud-native e-commerce platform built with ASP.NET Core 8.0 microservices, deployed to Azure Container Apps with a full observability stack.

[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Azure](https://img.shields.io/badge/Azure-Container%20Apps-blue)](https://azure.microsoft.com/services/container-apps/)
[![Docker](https://img.shields.io/badge/Docker-Enabled-2496ED)](https://www.docker.com/)
[![Deployment](https://img.shields.io/badge/Deployment-Live%20on%20Azure-success)](https://web.mangosea-a7508352.eastus.azurecontainerapps.io)

> **Demo:** [web.mangosea-a7508352.eastus.azurecontainerapps.io](https://web.mangosea-a7508352.eastus.azurecontainerapps.io)
>
> 6 microservices | 5 SQL databases | Redis + Seq + Jaeger observability | ~$21/month

> [!WARNING]
> The live deployment has been deactivated to reallocate cloud resources to my ongoing [Design-MCP](https://github.com/zahran001/Design-MCP) project. All code and deployment automation remain fully documented and reproducible.

---

## Architecture

<img width="23408" height="2909" alt="Azure Resource Visualizer" src="https://github.com/user-attachments/assets/bbf236b7-70db-4c93-a92d-87e8b271f59e" />

**6 independent microservices** with database-per-service isolation and event-driven async communication:

```
                        Internet
                           |
                     [Web MVC] (public)
                           |
          +--------+-------+-------+--------+
          |        |       |       |        |
       AuthAPI  ProductAPI CouponAPI CartAPI EmailAPI
          |        |       |       |        |
       [Auth DB] [Product DB] [Coupon DB] [Cart DB] [Email DB]

       Async: AuthAPI --[loguser queue]--> EmailAPI
              CartAPI --[emailshoppingcart queue]--> EmailAPI
```

| Service | Port | Purpose |
|---------|------|---------|
| **AuthAPI** | 7002 | JWT authentication, registration, role management |
| **ProductAPI** | 7000 | Product catalog CRUD |
| **CouponAPI** | 7001 | Discount coupon management |
| **ShoppingCartAPI** | 7003 | Cart operations, checkout |
| **EmailAPI** | -- | Async email notification consumer (Service Bus) |
| **Web MVC** | 7230 | Frontend (BFF pattern) |

---

## Tech Stack

| Layer | Technologies |
|-------|-------------|
| **Backend** | ASP.NET Core 8.0, Entity Framework Core 9.0, AutoMapper, JWT Bearer Auth |
| **Frontend** | ASP.NET Core MVC (BFF pattern), Razor Views |
| **Data** | Azure SQL Serverless (5 databases), Azure Service Bus (2 queues) |
| **Observability** | Serilog + Seq (logging), OpenTelemetry + Jaeger (tracing), Redis (caching), Correlation ID middleware |
| **Infrastructure** | Docker multi-stage builds, Azure Container Apps, Azure Container Registry |

---

## Solution Structure

```
E-commerce/
+-- E-commerce.Services.AuthAPI/           # Authentication & user management
+-- E-commerce.Services.ProductAPI/        # Product catalog
+-- E-commerce.Services.CouponAPI/         # Coupon system
+-- E-commerce.Services.ShoppingCartAPI/   # Shopping cart
+-- Ecommerce.Services.EmailAPI/           # Email notification consumer
+-- E-commerce.Web/                        # MVC frontend
+-- Ecommerce.MessageBus/                  # Shared Service Bus library
+-- E-commerce.Shared/                     # Shared observability (OpenTelemetry, Serilog, Correlation IDs)
+-- scripts/Prod/                          # Deployment automation (build, push, deploy, health checks)
+-- Docs/                                  # Extended documentation
```

Each service follows: `Controllers/ -> Service/ -> Data/ -> Models/Dto/`

---

## Getting Started

### Prerequisites

- .NET 8 SDK
- SQL Server 2022 (or Docker: `docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123!" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest`)
- Azure Service Bus namespace (required for EmailAPI)
- Docker Desktop (optional, for containerized deployment)

### 1. Clone and Configure

```bash
git clone https://github.com/zahran001/E-commerce.git
cd E-commerce
```

Configure User Secrets for each service (AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI):

```bash
cd E-commerce.Services.AuthAPI
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=E-commerce_Auth;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "ServiceBusConnectionString" "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key"
dotnet user-secrets set "ApiSettings:JwtOptions:Secret" "your-secret-at-least-32-characters-long"
cd ..
```

Repeat for each service with the appropriate database name (`E-commerce_Product`, `E-commerce_Coupon`, `E-commerce_ShoppingCart`, `E-commerce_Email`). Only AuthAPI, ShoppingCartAPI, and EmailAPI need the `ServiceBusConnectionString`.

### 2. Run

**Visual Studio:** Open `E-commerce.sln` -> set all 6 projects as startup -> F5

**CLI (6 terminals):**
```bash
cd E-commerce.Services.AuthAPI && dotnet run
cd E-commerce.Services.ProductAPI && dotnet run
cd E-commerce.Services.CouponAPI && dotnet run
cd E-commerce.Services.ShoppingCartAPI && dotnet run
cd Ecommerce.Services.EmailAPI && dotnet run
cd E-commerce.Web && dotnet run
```

**Docker Compose:** `docker-compose up -d`

Databases are created and seeded automatically on first startup.

### 3. Access

| | URL |
|--|-----|
| **Web UI** | https://localhost:7230 |
| **Swagger** | https://localhost:7000/swagger (Product), 7001 (Coupon), 7002 (Auth), 7003 (Cart) |
| **Seq** (optional) | `docker run -d -e ACCEPT_EULA=Y -p 5341:80 datalust/seq` -> http://localhost:5341 |
| **Jaeger** (optional) | `docker run -d -p 6831:6831/udp -p 16686:16686 jaegertracing/all-in-one:latest` -> http://localhost:16686 |

### 4. Test the Flow

1. Register a user -> Login (JWT stored in cookie)
2. Browse products (seeded: iPhone, TV, Headphones, Smartwatch)
3. Add to cart -> Apply coupon ("10OFF" or "20OFF")
4. Checkout (triggers email notification via Service Bus)

---

## References

### Logging and Tracing (Seq + Jaeger)
<img width="5760" height="2160" alt="Seq structured logging" src="https://github.com/user-attachments/assets/a5002d00-49dc-4de7-9a1b-6a2ddf294a35" />
<img width="5760" height="2160" alt="Jaeger distributed tracing" src="https://github.com/user-attachments/assets/0af0150b-f8c5-4506-aff7-42784821dc30" />

### Redis Caching (~95% latency reduction)

**Before:**
<img width="2880" height="1080" alt="Before Redis" src="https://github.com/user-attachments/assets/c2b6730d-f34c-48cc-82f5-07d788161201" />

**After:**
<img width="2880" height="1080" alt="After Redis" src="https://github.com/user-attachments/assets/d5e56651-099f-42a2-87d9-eb50d4ecc8c3" />

### Full Stack Running
<img width="5760" height="2160" alt="All services running" src="https://github.com/user-attachments/assets/5e088832-f25a-464b-ad8e-6f2b9aa0e690" />

---

## Future Enhancements

| Priority | Enhancement |
|----------|-------------|
| High | CI/CD Pipeline (GitHub Actions) |
| High | Polly resilience (retry, circuit breaker) |
| High | Email sending (SendGrid/Azure Communication Services) |
| High | Playwright E2E testing + xUnit |
| High | SignalR real-time updates (cart sync, inventory, order status) |
| Medium | Database indexing optimization |
| Medium | Admin & monitoring dashboards |
| Medium | Cloudflare Workers edge caching |
| Low | Azure Key Vault secrets management |

---

## Documentation

| Doc | Contents |
|-----|----------|
| [API Reference](Docs/API-REFERENCE.md) | All endpoints, request/response examples |
| [Docker Guide](Docs/DOCKER-GUIDE.md) | Build, compose, push to ACR |
| [Azure Deployment](Docs/AZURE-DEPLOYMENT.md) | Production architecture, Container Apps config, env vars |
| [Observability](Docs/OBSERVABILITY.md) | Correlation IDs, Serilog/Seq, OpenTelemetry/Jaeger, Redis |
| [Cost Analysis](Docs/COST-ANALYSIS.md) | Azure cost breakdown (~$21/month) |
| [CLAUDE.md](CLAUDE.md) | AI assistant context & detailed architecture |
| [Deployment Plan](Docs/PUSH-TO-PRODUCTION.md) | Step-by-step production deployment |

---

## Author

**Zahran** - [github.com/zahran001](https://github.com/zahran001)

---

## Acknowledgments

- Built with guidance from Microsoft's [eShopOnContainers](https://github.com/dotnet/eShop) reference architecture
- Inspired by microservices patterns from [microservices.io](https://microservices.io/)
- Started learning with [bhrugen/MagicVilla_API](https://github.com/bhrugen/MagicVilla_API)
