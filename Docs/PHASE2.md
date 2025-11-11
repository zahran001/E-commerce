# Phase 2: Containerization - Step-by-Step Guide

**Status:** 🔴 Not Started
**Estimated Time:** 1-1.5 hours (MVP) | 3-4 hours (Full)
**Goal:** Create Docker containers for all services and validate with docker-compose

**Prerequisites:**
- ✅ Phase 1 completed (secrets externalized)
- Docker Desktop installed and running
- Basic understanding of Docker concepts

---

## 🎯 Choose Your Approach

This guide offers **two paths** depending on your goals and timeline:

| Approach | Time | Best For | What You Get |
|----------|------|----------|--------------|
| **Solo-Dev MVP** | 1-1.5 hours | Solo developers, fast deployment | Production-ready images, minimal local setup |
| **Full Guide** | 3-4 hours | Learning, team environments | Complete local dev environment with NGINX + SQL |

### Quick Decision Guide

**Choose Solo-Dev MVP if:**
- ✅ You're working alone
- ✅ You want to deploy to Azure quickly
- ✅ You already have local SQL Server working
- ✅ You trust Azure Container Apps built-in features
- ✅ Your focus is on getting to production

**Choose Full Guide if:**
- ✅ You want to learn NGINX configuration
- ✅ You're building for a team environment
- ✅ You need complete local isolation (no external dependencies)
- ✅ You plan to deploy to Kubernetes (not Container Apps)
- ✅ You want maximum learning value

**Portfolio Value:** Both approaches demonstrate the same containerization skills to employers.

---

## 📍 Solo-Dev MVP Path (RECOMMENDED)

**Goal:** Create production-ready Docker images with minimal local complexity

### What You'll Build

```
Local Environment (MVP):
├── 6 Dockerfiles (production-ready)
├── docker-compose.yml (validation only)
└── Your existing local SQL Server (reused)

Azure Deployment (Phase 4):
├── Azure Container Apps (with built-in ingress)
├── Azure SQL Database
└── Azure Key Vault
```

### What You're Skipping (And Why)

| Component | Why Skip for MVP | When to Add |
|-----------|------------------|-------------|
| **NGINX Container** | Azure Container Apps has built-in ingress/routing | Phase 5 (if you need custom API gateway logic) |
| **SQL Server Container** | Your local SQL Server already works | Never (use Azure SQL in production) |
| **Complex Networking** | Docker auto-handles, Azure simplifies | Only if troubleshooting |
| **Extensive E2E Testing** | Validate in Azure instead | After first deployment |

### MVP Steps (1-1.5 hours)

**Step 1:** Create 6 Dockerfiles (30 min) → [Jump to Section](#step-2-create-dockerfiles-for-each-service)
**Step 2:** Create simplified docker-compose.yml (15 min) → [See MVP Compose](#mvp-docker-composeyml)
**Step 3:** Build & validate one service (20 min) → [Test Instructions](#mvp-testing)
**Step 4:** Document for Azure (10 min) → [Environment Variables](#mvp-env-vars)

**Then skip to Phase 4 (Azure Deployment)**

---

## 📚 Full Guide Path

**Goal:** Complete local development environment with all services containerized

### What You'll Build

```
Complete Local Environment:
├── 6 Dockerfiles
├── docker-compose.yml (full orchestration)
├── SQL Server container
├── NGINX reverse proxy
└── Custom Docker network
```

**Follow all steps below sequentially**

---

## ⚠️ Important Notes Before Starting

### What You'll Learn
- Multi-stage Dockerfile creation
- Container networking and service discovery
- Environment variable configuration
- Health checks in containers
- Local container orchestration with docker-compose

### What Changes
- Each service gets a Dockerfile
- Services will run on standardized port 8080 internally
- Configuration via environment variables
- Database will run in a container (SQL Server)
- NGINX will route all traffic

### What Stays the Same
- Application code (no changes needed)
- User Secrets for local non-Docker development
- Service-to-service communication patterns

---

## 📋 Phase 2 Overview

This phase will:
1. ✅ Create Dockerfiles for all 6 services (5 APIs + 1 Web MVC)
2. ✅ Create docker-compose.yml for local orchestration
3. ✅ Set up SQL Server container
4. ✅ Configure NGINX reverse proxy
5. ✅ Test the entire system running in containers
6. ✅ Validate health checks and service communication

---

## Step 1: Install and Verify Docker

### 1.1 Install Docker Desktop (if not already installed)

**Download:**
- Windows: https://www.docker.com/products/docker-desktop/
- Install with WSL 2 backend (recommended)

**Verify installation:**
```bash
docker --version
# Expected: Docker version 24.x.x or higher

docker-compose --version
# Expected: Docker Compose version v2.x.x or higher
```

### 1.2 Configure Docker Resources

**Open Docker Desktop → Settings → Resources:**
- **Memory:** At least 8 GB (12 GB recommended)
- **CPUs:** At least 4 cores
- **Disk:** At least 20 GB free

**Why?** Running 6 services + SQL Server + NGINX requires significant resources.

### 1.3 Test Docker is Working

```bash
# Test basic Docker functionality
docker run hello-world

# Expected: "Hello from Docker!" message
```

**✅ CHECKPOINT: Docker is installed and running**

---

## Step 2: Create Dockerfiles for Each Service

### 2.1 Understanding the Dockerfile Pattern

All our services will use a **multi-stage build** pattern:
1. **Stage 1 (base):** Runtime environment (aspnet:8.0)
2. **Stage 2 (build):** Build the application (sdk:8.0)
3. **Stage 3 (publish):** Publish release artifacts
4. **Stage 4 (final):** Copy artifacts to runtime image

**Benefits:**
- Smaller final image (only runtime, no SDK)
- Faster builds (cached layers)
- Security (minimal attack surface)

### 2.2 Create ProductAPI Dockerfile

**Create file:** `E-commerce.Services.ProductAPI\Dockerfile`

```dockerfile
# Multi-stage build for ProductAPI
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["E-commerce.Services.ProductAPI/E-commerce.Services.ProductAPI.csproj", "E-commerce.Services.ProductAPI/"]
RUN dotnet restore "E-commerce.Services.ProductAPI/E-commerce.Services.ProductAPI.csproj"

# Copy remaining files and build
COPY . .
WORKDIR "/src/E-commerce.Services.ProductAPI"
RUN dotnet build "E-commerce.Services.ProductAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "E-commerce.Services.ProductAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "E-commerce.Services.ProductAPI.dll"]
```

**Save this file** in `E-commerce.Services.ProductAPI\Dockerfile`

---

### 2.3 Create CouponAPI Dockerfile

**Create file:** `E-commerce.Services.CouponAPI\Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["E-commerce.Services.CouponAPI/E-commerce.Services.CouponAPI.csproj", "E-commerce.Services.CouponAPI/"]
RUN dotnet restore "E-commerce.Services.CouponAPI/E-commerce.Services.CouponAPI.csproj"
COPY . .
WORKDIR "/src/E-commerce.Services.CouponAPI"
RUN dotnet build "E-commerce.Services.CouponAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "E-commerce.Services.CouponAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "E-commerce.Services.CouponAPI.dll"]
```

---

### 2.4 Create AuthAPI Dockerfile

**Create file:** `E-commerce.Services.AuthAPI\Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["E-commerce.Services.AuthAPI/E-commerce.Services.AuthAPI.csproj", "E-commerce.Services.AuthAPI/"]
RUN dotnet restore "E-commerce.Services.AuthAPI/E-commerce.Services.AuthAPI.csproj"
COPY . .
WORKDIR "/src/E-commerce.Services.AuthAPI"
RUN dotnet build "E-commerce.Services.AuthAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "E-commerce.Services.AuthAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "E-commerce.Services.AuthAPI.dll"]
```

---

### 2.5 Create ShoppingCartAPI Dockerfile

**Note:** This service references Ecommerce.MessageBus project.

**Create file:** `E-commerce.Services.ShoppingCartAPI\Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy both ShoppingCartAPI and MessageBus projects
COPY ["E-commerce.Services.ShoppingCartAPI/E-commerce.Services.ShoppingCartAPI.csproj", "E-commerce.Services.ShoppingCartAPI/"]
COPY ["Ecommerce.MessageBus/Ecommerce.MessageBus.csproj", "Ecommerce.MessageBus/"]
RUN dotnet restore "E-commerce.Services.ShoppingCartAPI/E-commerce.Services.ShoppingCartAPI.csproj"

COPY . .
WORKDIR "/src/E-commerce.Services.ShoppingCartAPI"
RUN dotnet build "E-commerce.Services.ShoppingCartAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "E-commerce.Services.ShoppingCartAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "E-commerce.Services.ShoppingCartAPI.dll"]
```

---

### 2.6 Create EmailAPI Dockerfile

**Note:** This is a background service, but we'll still expose port 8080 for health checks.

**Create file:** `Ecommerce.Services.EmailAPI\Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy both EmailAPI and MessageBus projects
COPY ["Ecommerce.Services.EmailAPI/Ecommerce.Services.EmailAPI.csproj", "Ecommerce.Services.EmailAPI/"]
COPY ["Ecommerce.MessageBus/Ecommerce.MessageBus.csproj", "Ecommerce.MessageBus/"]
RUN dotnet restore "Ecommerce.Services.EmailAPI/Ecommerce.Services.EmailAPI.csproj"

COPY . .
WORKDIR "/src/Ecommerce.Services.EmailAPI"
RUN dotnet build "Ecommerce.Services.EmailAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ecommerce.Services.EmailAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Background service health check (less frequent)
HEALTHCHECK --interval=60s --timeout=5s --start-period=20s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Ecommerce.Services.EmailAPI.dll"]
```

---

### 2.7 Create Web MVC Dockerfile

**Create file:** `E-commerce.Web\Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["E-commerce.Web/E-commerce.Web.csproj", "E-commerce.Web/"]
RUN dotnet restore "E-commerce.Web/E-commerce.Web.csproj"
COPY . .
WORKDIR "/src/E-commerce.Web"
RUN dotnet build "E-commerce.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "E-commerce.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/ || exit 1
ENTRYPOINT ["dotnet", "E-commerce.Web.dll"]
```

---

### 2.8 Verification Checkpoint ✅

**Check that all Dockerfiles are created:**

```bash
# Run from repository root
ls E-commerce.Services.ProductAPI/Dockerfile
ls E-commerce.Services.CouponAPI/Dockerfile
ls E-commerce.Services.AuthAPI/Dockerfile
ls E-commerce.Services.ShoppingCartAPI/Dockerfile
ls Ecommerce.Services.EmailAPI/Dockerfile
ls E-commerce.Web/Dockerfile
```

**Expected:** All 6 files exist

**✅ CHECKPOINT: All Dockerfiles created**

---

## Step 3: Create NGINX Configuration

### 3.1 Create NGINX Folder and Configuration

**Create folder:**
```bash
mkdir nginx
```

**Create file:** `nginx\nginx.conf`

```nginx
events {
    worker_connections 1024;
}

http {
    # Upstream definitions (internal Docker network)
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

    # Main server block
    server {
        listen 80;
        server_name localhost;

        # Logging
        access_log /var/log/nginx/access.log;
        error_log /var/log/nginx/error.log warn;

        # API Gateway Pattern - Route by path prefix
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

        # Frontend application (default route)
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

### 3.2 Create NGINX Dockerfile

**Create file:** `nginx\Dockerfile`

```dockerfile
FROM nginx:alpine

# Copy custom NGINX configuration
COPY nginx/nginx.conf /etc/nginx/nginx.conf

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost/ || exit 1

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
```

**✅ CHECKPOINT: NGINX configuration created**

---

## Step 4: Create docker-compose.yml

### 4.1 Create Main Compose File

**Create file:** `docker-compose.yml` (in repository root)

```yaml
version: '3.8'

services:
  # SQL Server - Shared database container
  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: ecommerce-sql
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong!Passw0rd
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql
    networks:
      - ecommerce-network
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -Q "SELECT 1" || exit 1
      interval: 30s
      timeout: 3s
      retries: 10
      start_period: 10s

  # AuthAPI
  authapi:
    build:
      context: .
      dockerfile: E-commerce.Services.AuthAPI/Dockerfile
    container_name: ecommerce-authapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_Auth;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
      - ApiSettings__JwtOptions__Secret=local-dev-secret-min-32-chars-abcdefghijklmnopqrstuvwxyz123456
      - ApiSettings__JwtOptions__Issuer=e-commerce-auth-api
      - ApiSettings__JwtOptions__Audience=e-commerce-client
      - TopicAndQueueNames__LogUserQueue=loguser
      - ServiceBusConnectionString=${AZURE_SERVICEBUS_CONNECTION}
    ports:
      - "7002:8080"
    depends_on:
      sql-server:
        condition: service_healthy
    networks:
      - ecommerce-network

  # ProductAPI
  productapi:
    build:
      context: .
      dockerfile: E-commerce.Services.ProductAPI/Dockerfile
    container_name: ecommerce-productapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_Product;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
      - ApiSettings__Secret=local-dev-secret-min-32-chars-abcdefghijklmnopqrstuvwxyz123456
      - ApiSettings__Issuer=e-commerce-auth-api
      - ApiSettings__Audience=e-commerce-client
    ports:
      - "7000:8080"
    depends_on:
      sql-server:
        condition: service_healthy
    networks:
      - ecommerce-network

  # CouponAPI
  couponapi:
    build:
      context: .
      dockerfile: E-commerce.Services.CouponAPI/Dockerfile
    container_name: ecommerce-couponapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_Coupon;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
      - ApiSettings__Secret=local-dev-secret-min-32-chars-abcdefghijklmnopqrstuvwxyz123456
      - ApiSettings__Issuer=e-commerce-auth-api
      - ApiSettings__Audience=e-commerce-client
    ports:
      - "7001:8080"
    depends_on:
      sql-server:
        condition: service_healthy
    networks:
      - ecommerce-network

  # ShoppingCartAPI
  shoppingcartapi:
    build:
      context: .
      dockerfile: E-commerce.Services.ShoppingCartAPI/Dockerfile
    container_name: ecommerce-shoppingcartapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_ShoppingCart;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
      - ApiSettings__Secret=local-dev-secret-min-32-chars-abcdefghijklmnopqrstuvwxyz123456
      - ApiSettings__Issuer=e-commerce-auth-api
      - ApiSettings__Audience=e-commerce-client
      - ServiceUrls__ProductAPI=http://productapi:8080
      - ServiceUrls__CouponAPI=http://couponapi:8080
      - ServiceBusConnectionString=${AZURE_SERVICEBUS_CONNECTION}
      - TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart
    ports:
      - "7003:8080"
    depends_on:
      sql-server:
        condition: service_healthy
      productapi:
        condition: service_started
      couponapi:
        condition: service_started
    networks:
      - ecommerce-network

  # EmailAPI
  emailapi:
    build:
      context: .
      dockerfile: Ecommerce.Services.EmailAPI/Dockerfile
    container_name: ecommerce-emailapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=E-commerce_Email;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
      - ServiceBusConnectionString=${AZURE_SERVICEBUS_CONNECTION}
      - TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart
      - TopicAndQueueNames__LogUserQueue=loguser
    ports:
      - "7298:8080"
    depends_on:
      sql-server:
        condition: service_healthy
    networks:
      - ecommerce-network

  # Web MVC Frontend
  web:
    build:
      context: .
      dockerfile: E-commerce.Web/Dockerfile
    container_name: ecommerce-web
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
    networks:
      - ecommerce-network

  # NGINX Reverse Proxy
  nginx:
    build:
      context: .
      dockerfile: nginx/Dockerfile
    container_name: ecommerce-nginx
    ports:
      - "80:80"
    depends_on:
      - web
      - authapi
      - productapi
      - couponapi
      - shoppingcartapi
    networks:
      - ecommerce-network

# Docker volumes for persistent data
volumes:
  sqldata:
    driver: local

# Custom network for service communication
networks:
  ecommerce-network:
    driver: bridge
```

### 4.2 Create Environment File Template

**Create file:** `.env.example`

```env
# Azure Service Bus Connection String
# Get this from your Azure Portal → Service Bus → Shared access policies
AZURE_SERVICEBUS_CONNECTION=Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key-here

# SQL Server Password (used by docker-compose)
# Change this to a secure password in production
SA_PASSWORD=YourStrong!Passw0rd
```

**Create your actual .env file:**
```bash
# Copy the example file
cp .env.example .env

# Edit .env with your actual Azure Service Bus connection string
```

**Add to .gitignore:**
```bash
# Add this line to .gitignore if not already present
.env
```

**✅ CHECKPOINT: docker-compose.yml created**

---

## 🎯 MVP Docker-Compose.yml

**For Solo-Dev MVP approach:** Use this simplified version instead of the full compose file above.

### MVP docker-compose.yml

**Create file:** `docker-compose.yml` (in repository root)

```yaml
version: '3.8'

services:
  # AuthAPI
  authapi:
    build:
      context: .
      dockerfile: E-commerce.Services.AuthAPI/Dockerfile
    container_name: ecommerce-authapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      # Use host.docker.internal to connect to your local SQL Server
      - ConnectionStrings__DefaultConnection=Server=host.docker.internal;Database=E-commerce_Auth;Trusted_Connection=True;TrustServerCertificate=True
      - ApiSettings__JwtOptions__Secret=${JWT_SECRET}
      - ApiSettings__JwtOptions__Issuer=e-commerce-auth-api
      - ApiSettings__JwtOptions__Audience=e-commerce-client
      - TopicAndQueueNames__LogUserQueue=loguser
      - ServiceBusConnectionString=${AZURE_SERVICEBUS_CONNECTION}
    ports:
      - "7002:8080"

  # ProductAPI
  productapi:
    build:
      context: .
      dockerfile: E-commerce.Services.ProductAPI/Dockerfile
    container_name: ecommerce-productapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=host.docker.internal;Database=E-commerce_Product;Trusted_Connection=True;TrustServerCertificate=True
      - ApiSettings__Secret=${JWT_SECRET}
      - ApiSettings__Issuer=e-commerce-auth-api
      - ApiSettings__Audience=e-commerce-client
    ports:
      - "7000:8080"

  # CouponAPI
  couponapi:
    build:
      context: .
      dockerfile: E-commerce.Services.CouponAPI/Dockerfile
    container_name: ecommerce-couponapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=host.docker.internal;Database=E-commerce_Coupon;Trusted_Connection=True;TrustServerCertificate=True
      - ApiSettings__Secret=${JWT_SECRET}
      - ApiSettings__Issuer=e-commerce-auth-api
      - ApiSettings__Audience=e-commerce-client
    ports:
      - "7001:8080"

  # ShoppingCartAPI
  shoppingcartapi:
    build:
      context: .
      dockerfile: E-commerce.Services.ShoppingCartAPI/Dockerfile
    container_name: ecommerce-shoppingcartapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=host.docker.internal;Database=E-commerce_ShoppingCart;Trusted_Connection=True;TrustServerCertificate=True
      - ApiSettings__Secret=${JWT_SECRET}
      - ApiSettings__Issuer=e-commerce-auth-api
      - ApiSettings__Audience=e-commerce-client
      - ServiceUrls__ProductAPI=http://productapi:8080
      - ServiceUrls__CouponAPI=http://couponapi:8080
      - ServiceBusConnectionString=${AZURE_SERVICEBUS_CONNECTION}
      - TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart
    ports:
      - "7003:8080"
    depends_on:
      - productapi
      - couponapi

  # EmailAPI
  emailapi:
    build:
      context: .
      dockerfile: Ecommerce.Services.EmailAPI/Dockerfile
    container_name: ecommerce-emailapi
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=host.docker.internal;Database=E-commerce_Email;Trusted_Connection=True;TrustServerCertificate=True
      - ServiceBusConnectionString=${AZURE_SERVICEBUS_CONNECTION}
      - TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart
      - TopicAndQueueNames__LogUserQueue=loguser
    ports:
      - "7298:8080"

  # Web MVC Frontend
  web:
    build:
      context: .
      dockerfile: E-commerce.Web/Dockerfile
    container_name: ecommerce-web
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
```

### Key Differences from Full Compose

| Feature | MVP Compose | Full Compose | Reasoning |
|---------|-------------|--------------|-----------|
| **SQL Server** | Uses `host.docker.internal` | Runs SQL in container | Your local SQL already works, no need to containerize |
| **NGINX** | Not included | Included | Azure Container Apps provides ingress, NGINX adds complexity |
| **Networks** | Auto-created | Explicit network | Docker creates default network automatically |
| **Health Checks** | Not included | Included via `depends_on` conditions | Simpler startup, catch issues during build instead |
| **Volumes** | None | `sqldata` volume | No containerized database = no volume needed |

### MVP Environment File

**Create file:** `.env`

```env
# Azure Service Bus Connection String
AZURE_SERVICEBUS_CONNECTION=Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key-here

# JWT Secret (same as in User Secrets from Phase 1)
JWT_SECRET=local-dev-secret-min-32-chars-abcdefghijklmnopqrstuvwxyz123456
```

**Add to .gitignore:**
```
.env
```

### Why `host.docker.internal`?

**Problem:** Docker containers are isolated from your host machine.
**Solution:** `host.docker.internal` is a special DNS name that resolves to your host machine's IP.

**Connection string comparison:**
```
❌ Server=localhost;...
   ↑ This looks for SQL Server INSIDE the container (won't work)

✅ Server=host.docker.internal;...
   ↑ This connects to SQL Server on your Windows machine (works!)
```

**Note:** `Trusted_Connection=True` still works because the container inherits Windows authentication context.

---

## 🧪 MVP Testing

**For Solo-Dev MVP approach:** Follow these streamlined testing steps.

### Build One Service First

```bash
# Build just ProductAPI to test the pattern
docker-compose build productapi

# Expected: Successful build in 2-5 minutes
```

**If successful, build all services:**
```bash
docker-compose build

# This will take 10-15 minutes on first build
```

### Start and Test One Service

```bash
# Start just ProductAPI
docker-compose up productapi

# Expected output:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://[::]:8080
```

**In browser, test:**
- http://localhost:7000/swagger

**Should see:** Swagger UI with product endpoints

**Test GET /api/product:**
- Should return your seeded products

**Stop service:**
```bash
# Press Ctrl+C
```

### Test All Services (Optional)

```bash
# Start all 6 services
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f

# Stop all
docker-compose down
```

---

## 📝 MVP Environment Variables

**For Solo-Dev MVP approach:** Document these for Azure deployment (Phase 4).

### Environment Variables Checklist

**Create file:** `Docs/AZURE-ENV-VARS.md`

```markdown
# Azure Container Apps Environment Variables

## All Services (Common)
- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection` → From Azure Key Vault
- `ApiSettings__Secret` (or `ApiSettings__JwtOptions__Secret` for AuthAPI) → From Azure Key Vault

## AuthAPI
- `ApiSettings__JwtOptions__Issuer=e-commerce-auth-api`
- `ApiSettings__JwtOptions__Audience=e-commerce-client`
- `TopicAndQueueNames__LogUserQueue=loguser`
- `ServiceBusConnectionString` → From Azure Key Vault

## ProductAPI, CouponAPI
- `ApiSettings__Issuer=e-commerce-auth-api`
- `ApiSettings__Audience=e-commerce-client`

## ShoppingCartAPI
- `ApiSettings__Issuer=e-commerce-auth-api`
- `ApiSettings__Audience=e-commerce-client`
- `ServiceUrls__ProductAPI=https://productapi.internal.{env-id}.eastus.azurecontainerapps.io`
- `ServiceUrls__CouponAPI=https://couponapi.internal.{env-id}.eastus.azurecontainerapps.io`
- `ServiceBusConnectionString` → From Azure Key Vault
- `TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart`

## EmailAPI
- `ServiceBusConnectionString` → From Azure Key Vault
- `TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart`
- `TopicAndQueueNames__LogUserQueue=loguser`

## Web MVC
- `ServiceUrls__AuthAPI=https://authapi.internal.{env-id}.eastus.azurecontainerapps.io`
- `ServiceUrls__ProductAPI=https://productapi.internal.{env-id}.eastus.azurecontainerapps.io`
- `ServiceUrls__CouponAPI=https://couponapi.internal.{env-id}.eastus.azurecontainerapps.io`
- `ServiceUrls__ShoppingCartAPI=https://shoppingcartapi.internal.{env-id}.eastus.azurecontainerapps.io`
```

---

## ✅ MVP Phase 2 Complete!

**You've accomplished:**
- ✅ Created 6 production-ready Dockerfiles
- ✅ Created docker-compose.yml for local validation
- ✅ Built and tested containerized services
- ✅ Documented environment variables for Azure

**What you skipped (and why it's OK):**
- ❌ NGINX (Azure provides this)
- ❌ SQL Container (using local + Azure SQL later)
- ❌ Complex local setup (focus on Azure deployment)

**Time saved:** 2+ hours

**Next steps:**
1. Skip most of Phase 3 (only add basic `/health` endpoints)
2. Jump to Phase 4 (Azure Container Registry + Container Apps)
3. Add NGINX later if needed (Phase 5)

**Portfolio value:** ✅ Same as full approach (you containerized microservices)

---

## Step 5: Build and Test Docker Images

### 5.1 Build All Images

```bash
# From repository root
docker-compose build

# This will take 10-15 minutes on first build
# Watch for any errors during build
```

**What's happening:**
- Docker downloads base images (aspnet:8.0, sdk:8.0, nginx:alpine, SQL Server)
- Restores NuGet packages for each service
- Compiles each service
- Creates optimized production images

**Expected output:**
```
[+] Building 600.2s (123/123) FINISHED
...
Successfully built 7 images
```

**If you get errors:**
- Check Dockerfile paths are correct
- Ensure .csproj file names match exactly
- Verify Docker Desktop has enough memory

### 5.2 Verify Images Created

```bash
docker images | findstr ecommerce

# Expected output (7 images):
# ecommerce-nginx               latest    ...
# ecommerce-web                 latest    ...
# ecommerce-authapi             latest    ...
# ecommerce-productapi          latest    ...
# ecommerce-couponapi           latest    ...
# ecommerce-shoppingcartapi     latest    ...
# ecommerce-emailapi            latest    ...
```

**✅ CHECKPOINT: All images built successfully**

---

## Step 6: Start the Application

### 6.1 Start All Containers

```bash
# Start all services in detached mode (background)
docker-compose up -d

# Watch the startup logs
docker-compose logs -f
```

**Startup sequence (takes 2-3 minutes):**
1. SQL Server starts and initializes
2. APIs wait for SQL Server health check
3. APIs start and run migrations
4. Web MVC starts
5. NGINX starts

### 6.2 Monitor Startup Progress

**In a separate terminal, watch container status:**
```bash
docker-compose ps
```

**Expected output (all healthy):**
```
NAME                    STATUS              PORTS
ecommerce-authapi       Up (healthy)        0.0.0.0:7002->8080/tcp
ecommerce-couponapi     Up (healthy)        0.0.0.0:7001->8080/tcp
ecommerce-emailapi      Up (healthy)        0.0.0.0:7298->8080/tcp
ecommerce-nginx         Up (healthy)        0.0.0.0:80->80/tcp
ecommerce-productapi    Up (healthy)        0.0.0.0:7000->8080/tcp
ecommerce-shoppingcartapi Up (healthy)      0.0.0.0:7003->8080/tcp
ecommerce-sql           Up (healthy)        0.0.0.0:1433->1433/tcp
ecommerce-web           Up (healthy)        0.0.0.0:7230->8080/tcp
```

**If services are unhealthy:**
```bash
# Check logs for specific service
docker-compose logs authapi

# Common issues:
# - Database connection timeout (wait for SQL Server health check)
# - Missing environment variable
# - Service Bus connection string not set in .env
```

**✅ CHECKPOINT: All containers running and healthy**

---

## Step 7: Test the Application

### 7.1 Test Direct Service Access

**Open browser and test each service:**

1. **ProductAPI:** http://localhost:7000/swagger
   - Should show Swagger UI
   - Try GET /api/product
   - Should return products

2. **CouponAPI:** http://localhost:7001/swagger
   - Should show Swagger UI
   - Try GET /api/coupon
   - Should return coupons

3. **AuthAPI:** http://localhost:7002/swagger
   - Should show Swagger UI
   - Try POST /api/auth/register (create test user)

4. **ShoppingCartAPI:** http://localhost:7003/swagger
   - Should show Swagger UI (requires JWT token to test)

5. **Web MVC:** http://localhost:7230
   - Should show home page
   - Should display products

### 7.2 Test NGINX Routing

**Test API Gateway pattern:**

```bash
# Test ProductAPI through NGINX
curl http://localhost/api/product/

# Expected: JSON array of products

# Test CouponAPI through NGINX
curl http://localhost/api/coupon/

# Expected: JSON array of coupons

# Test Web UI through NGINX
curl http://localhost/

# Expected: HTML content
```

### 7.3 Test Full E2E Flow

**Complete user journey:**

1. **Open Web UI:** http://localhost/
2. **View Products:** Should see product catalog
3. **Register User:** Click Register, create account
   - This triggers EmailAPI via Service Bus
4. **Login:** Use registered credentials
5. **Add to Cart:** Select products, add to cart
6. **Apply Coupon:** Use "10OFF" or "20OFF"
7. **Checkout:** Complete checkout
   - This triggers EmailAPI via Service Bus

**Check EmailAPI logs:**
```bash
docker-compose logs emailapi | findstr "Received"

# Should show:
# - User registration message received
# - Shopping cart email message received
```

### 7.4 Test Service-to-Service Communication

**ShoppingCartAPI should call ProductAPI and CouponAPI:**

```bash
# Watch ShoppingCartAPI logs
docker-compose logs -f shoppingcartapi

# Add item to cart via Web UI
# Logs should show HTTP calls to ProductAPI and CouponAPI
```

**✅ CHECKPOINT: All services communicating correctly**

---

## Step 8: Verify Database Migrations

### 8.1 Connect to SQL Server Container

```bash
# Connect to SQL Server
docker exec -it ecommerce-sql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd"
```

### 8.2 Check Databases Created

```sql
-- List all databases
SELECT name FROM sys.databases;
GO

-- Expected output:
-- E-commerce_Auth
-- E-commerce_Product
-- E-commerce_Coupon
-- E-commerce_ShoppingCart
-- E-commerce_Email
```

### 8.3 Check Seed Data

```sql
-- Check products
USE [E-commerce_Product]
GO
SELECT * FROM Products;
GO

-- Check coupons
USE [E-commerce_Coupon]
GO
SELECT * FROM Coupons;
GO

-- Exit
quit
```

**✅ CHECKPOINT: All databases migrated and seeded**

---

## Step 9: Health Checks and Monitoring

### 9.1 Check Container Health

```bash
# View health status
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

### 9.2 Monitor Resource Usage

```bash
# View resource consumption
docker stats

# Expected:
# - Each API: ~100-200 MB RAM, <5% CPU (idle)
# - SQL Server: ~500-800 MB RAM
# - NGINX: ~10-20 MB RAM
# - Web MVC: ~100-150 MB RAM
```

### 9.3 Check Logs for Errors

```bash
# Check all logs for errors
docker-compose logs | findstr "error"

# Should return minimal results (no critical errors)
```

**✅ CHECKPOINT: System healthy and performing well**

---

## Step 10: Cleanup and Management

### 10.1 Stop All Containers

```bash
# Stop all services (preserves volumes)
docker-compose down
```

### 10.2 Stop and Remove Volumes

```bash
# Stop and remove everything (including database data)
docker-compose down -v

# WARNING: This deletes all database data!
```

### 10.3 Rebuild After Code Changes

```bash
# Rebuild specific service
docker-compose build authapi

# Restart that service
docker-compose up -d authapi

# Or rebuild everything
docker-compose down
docker-compose build
docker-compose up -d
```

### 10.4 View Logs for Debugging

```bash
# Follow logs for all services
docker-compose logs -f

# Logs for specific service
docker-compose logs -f productapi

# Last 100 lines
docker-compose logs --tail=100 authapi
```

---

## 🎉 Phase 2 Complete!

### What You Accomplished

✅ **Containerization:**
- Created Dockerfiles for all 6 services
- Multi-stage builds for optimized images
- Health checks for all containers

✅ **Orchestration:**
- docker-compose.yml for local development
- SQL Server running in container
- NGINX reverse proxy configured
- Custom Docker network for service communication

✅ **Testing:**
- All services start and communicate
- Database migrations auto-apply
- Service Bus messaging works
- API Gateway routing functional

✅ **DevOps Foundation:**
- Images ready for Azure Container Registry
- Configuration externalized via environment variables
- Health checks ready for production monitoring

### Project Structure Now

```
E-commerce/
├── E-commerce.Services.AuthAPI/
│   └── Dockerfile ✅
├── E-commerce.Services.ProductAPI/
│   └── Dockerfile ✅
├── E-commerce.Services.CouponAPI/
│   └── Dockerfile ✅
├── E-commerce.Services.ShoppingCartAPI/
│   └── Dockerfile ✅
├── Ecommerce.Services.EmailAPI/
│   └── Dockerfile ✅
├── E-commerce.Web/
│   └── Dockerfile ✅
├── nginx/
│   ├── Dockerfile ✅
│   └── nginx.conf ✅
├── docker-compose.yml ✅
├── .env (your secrets) ✅
└── .env.example ✅
```

---

## 🔧 Troubleshooting

### Issue: Container won't start

**Error:** `exited with code 1`

**Solution:**
```bash
# Check logs
docker-compose logs servicename

# Common causes:
# 1. Missing environment variable
# 2. Database not ready (wait for health check)
# 3. Port already in use
```

---

### Issue: SQL Server not healthy

**Error:** `container is unhealthy`

**Solution:**
```bash
# Check SQL Server logs
docker-compose logs sql-server

# Increase timeout in docker-compose.yml:
# healthcheck:
#   retries: 20  # ← Increase from 10
#   start_period: 30s  # ← Increase from 10s
```

---

### Issue: Migration fails

**Error:** `Failed executing DbCommand`

**Solution:**
```bash
# Restart SQL Server container
docker-compose restart sql-server

# Or rebuild database
docker-compose down -v
docker-compose up -d
```

---

### Issue: Service Bus messages not flowing

**Error:** `Unauthorized. 'Send' claim(s) are required`

**Check:**
1. Is `AZURE_SERVICEBUS_CONNECTION` set in `.env`?
2. Does connection string have Send/Listen permissions?
3. Are queue names correct in configuration?

**Solution:**
```bash
# Check .env file
cat .env

# Restart EmailAPI
docker-compose restart emailapi
```

---

### Issue: NGINX shows 502 Bad Gateway

**Error:** `502 Bad Gateway`

**Solution:**
```bash
# Check upstream services are running
docker-compose ps

# Check NGINX logs
docker-compose logs nginx

# Verify service names in nginx.conf match docker-compose.yml
```

---

## 📊 Performance Benchmarks

**Startup time:** 2-3 minutes (cold start)
**Memory usage:** ~2.5 GB total
**Image sizes:**
- APIs: ~220 MB each
- Web MVC: ~230 MB
- NGINX: ~40 MB
- SQL Server: ~1.5 GB

---

## 📚 Next Steps

**Phase 3: Production-Ready Enhancements**
- Add structured logging (Serilog)
- Implement health check endpoints
- Add Application Insights
- Configure resilience patterns (Polly)

**Estimated time for Phase 3:** 2-3 hours

---

## 🆘 Quick Commands Reference

```bash
# Build all images
docker-compose build

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Check status
docker-compose ps

# Stop all services
docker-compose down

# Rebuild specific service
docker-compose build authapi && docker-compose up -d authapi

# Remove everything (including volumes)
docker-compose down -v

# View resource usage
docker stats
```

---

## 📖 Decision Rationale Reference

**Keep this section for future reference when explaining your architecture decisions.**

### Why Multi-Stage Builds?

**Decision:** Use 4-stage Dockerfiles (base → build → publish → final)

**Reasoning:**
1. **Image Size:** 220 MB vs 1.2 GB (5.5x reduction)
   - Final image contains only ASP.NET runtime, not SDK
   - Faster push/pull times to container registry
   - Lower bandwidth costs

2. **Security:** Minimal attack surface
   - No source code in production image
   - No build tools or compilers
   - Only runtime dependencies

3. **Build Performance:** Layer caching
   - Dependencies cached separately from code
   - Code changes don't invalidate package restore
   - 10-second rebuilds vs 5-minute full builds

4. **Production Best Practice:**
   - Industry standard for .NET containers
   - Microsoft's official recommendation
   - Demonstrates optimization skills to employers

**Trade-off:** Slightly more complex Dockerfile, but worth it for production deployments.

---

### Why Skip NGINX in MVP?

**Decision:** Use Azure Container Apps built-in ingress instead of NGINX reverse proxy

**Reasoning:**
1. **Azure Already Provides:**
   - HTTPS/SSL termination (free managed certificates)
   - Load balancing across replicas
   - Health-based routing
   - Automatic failover

2. **Complexity Reduction:**
   - One less container to manage
   - Simpler debugging (fewer hops)
   - Faster local development

3. **Cost:**
   - No extra Container App needed (~$10/month saved)
   - Less memory usage

4. **Portfolio Value:**
   - Both approaches demonstrate the same core skills
   - Employers care that you containerized microservices
   - They don't care whether you used NGINX locally

**When to Add NGINX Later:**
- Need custom routing logic (beyond path-based)
- Need rate limiting (can also use Azure Front Door)
- Need request transformation
- Deploying to Kubernetes (not Container Apps)

**How to Upgrade:** Add NGINX container in Phase 5 without changing any service Dockerfiles.

---

### Why Skip SQL Server Container?

**Decision:** Use `host.docker.internal` to connect to local SQL Server

**Reasoning:**
1. **Your Local SQL Already Works:**
   - Databases already created and seeded
   - User Secrets already configured
   - No migration risk

2. **Production Alignment:**
   - You'll use Azure SQL Database (not containerized SQL)
   - Local container doesn't match production
   - Better to test against actual SQL Server

3. **Resource Usage:**
   - SQL Server container uses 500-800 MB RAM
   - Slower startup (30-60 seconds)
   - Unnecessary complexity for validation

4. **Development Workflow:**
   - Easier to query databases with SSMS
   - Easier to reset data
   - Faster container startup

**Trade-off:** Containers depend on host SQL Server (less isolated), but acceptable for MVP.

---

### Why `host.docker.internal`?

**Technical Detail:** How containers connect to host services

**Problem:**
```
Docker Container (isolated network)
├── Can't access localhost (looks inside container)
├── Can't access 127.0.0.1 (container's own loopback)
└── Can't access host IP (dynamic, unknown)
```

**Solution:**
```
Docker Desktop provides:
host.docker.internal → Resolves to host machine's IP

Connection String:
Server=host.docker.internal;Database=E-commerce_Product;...
         ↑
         Special DNS name added by Docker Desktop
```

**Why Trusted_Connection Works:**
- Docker Desktop on Windows passes Windows auth context
- Container inherits your Windows user credentials
- No need for SQL username/password

**Linux Note:** On Linux, use `--add-host=host.docker.internal:host-gateway` in docker-compose.

---

### Why Environment Variables Instead of User Secrets?

**Decision:** Use environment variables in docker-compose, not User Secrets

**Reasoning:**
1. **Containers Don't Support User Secrets:**
   - User Secrets stored in `%APPDATA%` (host file system)
   - Containers can't access host file system by default
   - Would need volume mounts (adds complexity)

2. **12-Factor App Principle:**
   - Config via environment variables (industry standard)
   - Same pattern for local, staging, production
   - Easier to override per environment

3. **Docker Best Practice:**
   - docker-compose.yml defines environment variables
   - `.env` file for local secrets (gitignored)
   - Same pattern works in Kubernetes, Azure, AWS

**Relationship to Phase 1:**
- User Secrets: For local non-Docker development
- Environment Variables: For containerized environments
- Azure Key Vault: For production (Phase 4)

---

### Why Skip Extensive E2E Testing?

**Decision:** Validate basic functionality, defer full testing to Azure

**Reasoning:**
1. **MVP Focus:**
   - Goal: Validate containers work, not test application
   - Application already tested in Phase 1 (non-Docker)
   - Same code, different runtime

2. **Environment Differences:**
   - Local uses `host.docker.internal` (not production)
   - Local uses User Secrets (production uses Key Vault)
   - Better to test in actual Azure environment

3. **Time Investment:**
   - Full E2E test: 1-2 hours
   - Marginal value (application already works)
   - Focus on deployment instead

**What You Should Test:**
- ✅ Each service builds successfully
- ✅ Each service starts without errors
- ✅ One API endpoint returns data (validates DB connection)
- ❌ Full user journey (defer to Azure)
- ❌ Service Bus messaging (defer to Azure)

---

### Multi-Stage Build Stage Selection

**How We Chose 4 Stages:**

| Stage | Could Skip? | Why We Keep It |
|-------|-------------|----------------|
| **base** | Yes (reference aspnet:8.0 directly in final) | Clarity - explicitly defines runtime environment |
| **build** | No | Required for compilation |
| **publish** | Yes (publish in build stage) | Separation of concerns - build artifacts vs optimized output |
| **final** | No | Required final image |

**Alternative: 3 Stages (also valid)**
```dockerfile
FROM sdk AS build
RUN dotnet build && dotnet publish

FROM aspnet  # ← Skip "base" stage
COPY --from=build /app/publish .
```

**Why we use 4:** Better organization and Microsoft's recommended pattern.

---

### Portfolio Impact Analysis

**Will employers value MVP approach as much as Full approach?**

**What Employers Care About:**
1. ✅ Can you containerize applications? (Both demonstrate this)
2. ✅ Do you understand multi-stage builds? (Both use same Dockerfiles)
3. ✅ Can you deploy to cloud? (MVP gets you there faster)
4. ✅ Do you make pragmatic decisions? (MVP shows prioritization skills)

**What Employers Don't Care About:**
- ❌ Whether you used NGINX locally
- ❌ Whether you containerized SQL Server
- ❌ How much time you spent on local orchestration

**In Interviews:**
- **Both approaches:** "I containerized 6 microservices with multi-stage builds"
- **MVP advantage:** "I optimized for deployment speed by leveraging Azure features"
- **Full advantage:** "I built complete local development environment"

**Verdict:** MVP approach is equally valuable, possibly more so (shows decision-making).

---

## 🎓 Key Learnings Summary

### Technical Skills Demonstrated

**After Phase 2, you can confidently discuss:**

1. **Multi-Stage Docker Builds**
   - Why SDK and Runtime images differ
   - How to optimize image size (220 MB vs 1.2 GB)
   - Layer caching strategy for faster builds

2. **Container Orchestration**
   - Service-to-service communication in Docker
   - Environment-based configuration
   - Dependency management (depends_on)

3. **Cloud-Native Patterns**
   - 12-Factor App (config via env vars)
   - Container-to-host networking (host.docker.internal)
   - Stateless services (ready for horizontal scaling)

4. **Production Readiness**
   - Optimized images for production
   - Health checks (even if skipped in MVP)
   - Security (no secrets in images)

### Architecture Decisions You Can Defend

**In interviews, when asked "Why didn't you use X?":**

1. **"Why no NGINX?"**
   - "Azure Container Apps provides ingress, I leveraged platform features instead of adding complexity"

2. **"Why not containerize SQL?"**
   - "Production uses Azure SQL (not containerized), I optimized for production alignment"

3. **"Why 4-stage build?"**
   - "Image size optimization (5.5x reduction) and security (no SDK in production)"

4. **"Why skip extensive testing?"**
   - "Validated basic functionality, deferred full testing to production-like environment (Azure)"

---

**Last Updated:** 2025-11-11
**Time Investment:**
- Solo-Dev MVP: 1-1.5 hours
- Full Approach: 3-4 hours
**Next Phase:** Production-Ready Enhancements (PHASE3.md) or Azure Deployment (PHASE4.md)
