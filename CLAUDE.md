# CLAUDE.md - E-commerce Microservices Project Guide

This document provides AI assistants (particularly Claude) with essential context about this E-commerce microservices project.

## Project Overview

This is a **microservices-based e-commerce platform** built with ASP.NET Core 8.0, featuring:
- 5 independent microservices (Auth, Product, Coupon, ShoppingCart, Email)
- Event-driven architecture using Azure Service Bus
- JWT-based authentication & authorization
- Database-per-service pattern with SQL Server
- ASP.NET Core MVC frontend

## Architecture

### Microservices

| Service | Port | Database | Purpose |
|---------|------|----------|---------|
| **AuthAPI** | 7002 | E-commerce_Auth | User authentication, registration, JWT generation, role management |
| **ProductAPI** | 7000 | E-commerce_Product | Product catalog management (CRUD) |
| **CouponAPI** | 7001 | E-commerce_Coupon | Discount coupon management |
| **ShoppingCartAPI** | 7003 | E-commerce_ShoppingCart | Shopping cart operations, cart checkout |
| **EmailAPI** | N/A | E-commerce_Email | Async email notification consumer (Service Bus) |
| **Web (MVC)** | N/A | None | Frontend application (BFF pattern) |

### Communication Patterns

**Synchronous (HTTP/REST):**
- Web → All APIs (user-initiated requests)
- ShoppingCartAPI → ProductAPI (fetch product details)
- ShoppingCartAPI → CouponAPI (validate coupons)

**Asynchronous (Azure Service Bus):**
- AuthAPI → EmailAPI (user registration notifications via `loguser` queue)
- ShoppingCartAPI → EmailAPI (cart email requests via `emailshoppingcart` queue)

### Message Bus Queues

| Queue Name | Publisher | Consumer | Message Type | Purpose |
|------------|-----------|----------|--------------|---------|
| `loguser` | AuthAPI | EmailAPI | string (email) | Log new user registrations |
| `emailshoppingcart` | ShoppingCartAPI | EmailAPI | CartDto | Send cart summary emails |

## Project Structure

```
E-commerce/
├── E-commerce.Services.AuthAPI/           # Authentication service
├── E-commerce.Services.ProductAPI/        # Product catalog service
├── E-commerce.Services.CouponAPI/         # Coupon management service
├── E-commerce.Services.ShoppingCartAPI/   # Shopping cart service
├── Ecommerce.Services.EmailAPI/           # Email notification consumer
├── E-commerce.Web/                        # MVC frontend
├── Ecommerce.MessageBus/                  # Shared Service Bus library
└── CLAUDE.md                              # This file
```

## Common Service Structure

Each microservice follows this consistent pattern:

```
ServiceAPI/
├── Controllers/          # API endpoints (RESTful)
├── Data/                 # ApplicationDbContext (EF Core)
├── Models/               # Domain entities
│   └── Dto/             # Data Transfer Objects
├── Service/             # Business logic layer
│   └── IService/        # Service interfaces
├── Extensions/          # Extension methods (JWT config, etc.)
├── Utility/             # Helper classes
├── MappingConfig.cs     # AutoMapper profile
├── Program.cs           # Startup configuration
└── appsettings.json     # Configuration
```

## Key Technologies

- **Framework:** ASP.NET Core 8.0 (.NET 8)
- **Database:** SQL Server (Server=ZAHRAN, local instance)
- **ORM:** Entity Framework Core 9.0
- **Authentication:** ASP.NET Core Identity (AuthAPI), JWT Bearer
- **Messaging:** Azure Service Bus (ecommerceweb.servicebus.windows.net)
- **Object Mapping:** AutoMapper 13.0.1
- **API Documentation:** Swagger/Swashbuckle
- **Serialization:** Newtonsoft.Json 13.0.3

## Development Patterns & Conventions

### 1. ResponseDto Pattern

**All APIs return standardized responses:**

```csharp
public class ResponseDto
{
    public object Result { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string Message { get; set; } = "";
}
```

**Usage in controllers:**
```csharp
return Ok(new ResponseDto { Result = data, IsSuccess = true });
// or
return BadRequest(new ResponseDto { IsSuccess = false, Message = "Error message" });
```

### 2. Automatic Database Migrations

Every service auto-applies pending migrations on startup via `ApplyMigration()` in [Program.cs](E-commerce.Services.AuthAPI/Program.cs):

```csharp
void ApplyMigration()
{
    using (var scope = app.Services.CreateScope())
    {
        var _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (_db.Database.GetPendingMigrations().Count() > 0)
        {
            _db.Database.Migrate();
        }
    }
}
```

### 3. JWT Authentication Extension

Reusable authentication configuration via `AddAppAuthentication()` extension method (found in ProductAPI, CouponAPI, ShoppingCartAPI):

```csharp
// In Program.cs
builder.AddAppAuthentication();
```

**Configuration (appsettings.json):**
```json
{
  "ApiSettings": {
    "Secret": "A ninja must always be prepared Scooby Dooby Doo VROOM VROOM Pumpkin Muncher",
    "Issuer": "e-commerce-auth-api",
    "Audience": "e-commerce-client"
  }
}
```

### 4. AutoMapper Configuration

Static configuration pattern:

```csharp
// In Program.cs
IMapper mapper = MappingConfig.RegisterMaps().CreateMapper();
builder.Services.AddSingleton(mapper);
```

### 5. Service Dependency Injection

**Standard scoping:**
- **Scoped:** Controllers, Services, DbContext
- **Singleton:** AutoMapper, MessageBus
- **Special Case:** EmailAPI uses singleton EmailService with DbContextOptions

## Authentication & Authorization

### JWT Token Flow

1. **User logs in** → [AuthAPI/Controllers/AuthAPIController.cs:Login](E-commerce.Services.AuthAPI/Controllers/AuthAPIController.cs)
2. **AuthService validates credentials** → [Service/AuthService.cs](E-commerce.Services.AuthAPI/Service/AuthService.cs)
3. **JwtTokenGenerator creates token** → [Service/JwtTokenGenerator.cs](E-commerce.Services.AuthAPI/Service/JwtTokenGenerator.cs)
4. **Token returned to client** (stored in cookie by Web MVC)
5. **Subsequent requests include token** in Authorization header

### Token Details

- **Algorithm:** HMAC SHA256 (symmetric)
- **Expiration:** 7 days
- **Claims:** Email, Sub (User ID), Name, Role(s)
- **Secret:** Configured in appsettings.json (should move to Azure Key Vault)

### Roles

- **ADMIN:** Full access to all endpoints
- **CUSTOMER:** Read-only access, can manage own cart

**Admin-only endpoints:**
- POST/PUT/DELETE on ProductAPI
- POST/PUT/DELETE on CouponAPI
- AssignRole on AuthAPI

### Token Propagation (Service-to-Service)

ShoppingCartAPI uses [BackendAPIAuthenticationHttpClientHandler.cs](E-commerce.Services.ShoppingCartAPI/Utility/BackendAPIAuthenticationHttpClientHandler.cs) to propagate JWT when calling ProductAPI/CouponAPI:

```csharp
builder.Services.AddHttpClient("Product", u => u.BaseAddress =
    new Uri(builder.Configuration["ServiceUrls:ProductAPI"]))
    .AddHttpMessageHandler<BackendAPIAuthenticationHttpClientHandler>();
```

## Database Setup

### Connection Strings

**Pattern:** `Server=ZAHRAN;Database=E-commerce_[ServiceName];Trusted_Connection=True;TrustServerCertificate=True`

### Databases

| Database | Tables | Notes |
|----------|--------|-------|
| **E-commerce_Auth** | AspNetUsers, AspNetRoles, AspNetUserRoles, ApplicationUsers | Identity framework tables |
| **E-commerce_Product** | Products | Seeded with 4 products (iPhone, TV, Headphones, Smartwatch) |
| **E-commerce_Coupon** | Coupons | Seeded with "10OFF", "20OFF" |
| **E-commerce_ShoppingCart** | CartHeaders, CartDetails | CartDetails has FK to CartHeaders, stores ProductId only |
| **E-commerce_Email** | EmailLoggers | Logs all email notifications (not actual sending yet) |

### EF Core Conventions

- **DbContext naming:** Always `ApplicationDbContext`
- **Seed data:** Configured in `OnModelCreating()`
- **Migrations:** Auto-applied on startup
- **Navigation properties:** Used minimally (microservices prefer data duplication)

## Message Bus Implementation

### Publishing Messages

**Register MessageBus in DI:**
```csharp
builder.Services.AddScoped<IMessageBus, MessageBus>();
```

**Publish from controller:**
```csharp
await _messageBus.PublishMessage(messageObject, queueName);
```

**Configuration (appsettings.json):**
```json
{
  "TopicAndQueueNames": {
    "EmailShoppingCartQueue": "emailshoppingcart",
    "LogUserQueue": "loguser"
  }
}
```

### Consuming Messages (EmailAPI)

**AzureServiceBusConsumer** ([Services/AzureServiceBusConsumer.cs](Ecommerce.Services.EmailAPI/Services/AzureServiceBusConsumer.cs)):
- Creates ServiceBusProcessor for each queue
- Registers message handlers
- Managed via `UseAzureServiceBusConsumer()` extension

**Lifecycle:**
```csharp
// In Program.cs
app.UseAzureServiceBusConsumer(); // Starts consumers on app start
```

## Configuration Management

### appsettings.json Structure

**AuthAPI:**
```json
{
  "ApiSettings": {
    "JwtOptions": { "Secret": "...", "Issuer": "...", "Audience": "..." }
  },
  "TopicAndQueueNames": { "LogUserQueue": "loguser" }
}
```

**ShoppingCartAPI:**
```json
{
  "ApiSettings": { /* JWT validation */ },
  "ServiceUrls": {
    "ProductAPI": "https://localhost:7000",
    "CouponAPI": "https://localhost:7001"
  },
  "TopicAndQueueNames": { "EmailShoppingCartQueue": "emailshoppingcart" }
}
```

**EmailAPI:**
```json
{
  "ServiceBusConnectionString": "Endpoint=sb://ecommerceweb...",
  "TopicAndQueueNames": {
    "EmailShoppingCartQueue": "emailshoppingcart",
    "LogUserQueue": "loguser"
  }
}
```

**Web MVC:**
```json
{
  "ServiceUrls": {
    "CouponAPI": "https://localhost:7001",
    "AuthAPI": "https://localhost:7002",
    "ProductAPI": "https://localhost:7000",
    "ShoppingCartAPI": "https://localhost:7003"
  }
}
```

### Configuration Binding Patterns

**Strongly-typed options:**
```csharp
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("ApiSettings:JwtOptions"));
```

**Direct access:**
```csharp
var queueName = _configuration.GetValue<string>("TopicAndQueueNames:LogUserQueue");
```

**Static class (Web MVC):**
```csharp
StaticDetails.AuthApiBase = builder.Configuration["ServiceUrls:AuthAPI"];
```

## Common Tasks

### Adding a New Microservice

1. **Create new ASP.NET Core Web API project**
2. **Follow standard structure:**
   - Controllers/ for API endpoints
   - Models/ and Models/Dto/ for entities and DTOs
   - Data/ with ApplicationDbContext
   - Service/ and Service/IService/ for business logic
3. **Add packages:**
   ```bash
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer
   dotnet add package AutoMapper
   dotnet add package Swashbuckle.AspNetCore
   dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
   ```
4. **Configure in Program.cs:**
   - Add DbContext with connection string
   - Add AutoMapper with MappingConfig
   - Add JWT authentication via `AddAppAuthentication()` extension
   - Add Swagger
   - Add `ApplyMigration()` method
5. **Create initial migration:**
   ```bash
   dotnet ef migrations add InitialCreate
   ```
6. **Update Web MVC appsettings.json** with new service URL
7. **Create service client in Web** (Models/ServiceAPI.cs, Service/ServiceAPIService.cs)

### Adding a New API Endpoint

1. **Create/Update DTO** in Models/Dto/
2. **Add controller method** with [HttpGet]/[HttpPost]/etc.
3. **Use ResponseDto** for return type
4. **Add business logic** in Service layer if complex
5. **Add AutoMapper mapping** if needed in MappingConfig.cs
6. **Add authorization attribute** if required: `[Authorize]` or `[Authorize(Roles = "ADMIN")]`

### Publishing a New Message Bus Event

1. **Define message DTO** (or use primitive type)
2. **Add queue/topic name to appsettings.json:**
   ```json
   "TopicAndQueueNames": { "NewEventQueue": "newevent" }
   ```
3. **Inject IMessageBus** in controller/service
4. **Publish message:**
   ```csharp
   await _messageBus.PublishMessage(messageDto,
       _configuration.GetValue<string>("TopicAndQueueNames:NewEventQueue"));
   ```
5. **Update EmailAPI** (or create new consumer service):
   - Add queue name to appsettings.json
   - Add new ServiceBusProcessor in AzureServiceBusConsumer
   - Implement message handler method
   - Register processor in constructor

### Adding a New Role/Authorization Rule

1. **Define role constant** in [StaticDetails.cs](E-commerce.Services.AuthAPI/Utility/StaticDetails.cs) (if not exists)
2. **Seed role** in [ApplicationDbContext.cs](E-commerce.Services.AuthAPI/Data/ApplicationDbContext.cs) OnModelCreating
3. **Use in controller:**
   ```csharp
   [Authorize(Roles = "NEWROLE")]
   ```
4. **Assign to users** via [AssignRole endpoint](E-commerce.Services.AuthAPI/Controllers/AuthAPIController.cs)

### Running Migrations

**Add migration:**
```bash
cd E-commerce.Services.[ServiceName]API
dotnet ef migrations add MigrationName
```

**Apply migration:**
- Automatic on startup (via ApplyMigration() method)
- Or manually: `dotnet ef database update`

**Remove last migration:**
```bash
dotnet ef migrations remove
```

### Testing the Application

**Start all services:**
1. Run each API project (5 services)
2. Run Web MVC
3. Ensure Azure Service Bus is accessible

**Swagger endpoints:**
- ProductAPI: https://localhost:7000/swagger
- CouponAPI: https://localhost:7001/swagger
- AuthAPI: https://localhost:7002/swagger
- ShoppingCartAPI: https://localhost:7003/swagger

**Default users (seed data if configured):**
- Check [ApplicationDbContext.cs](E-commerce.Services.AuthAPI/Data/ApplicationDbContext.cs) in AuthAPI

## Current Limitations & TODOs

### Known Limitations

1. **No API Gateway** - Gateway folder exists but empty (future: implement Ocelot or YARP)
2. **Hardcoded secrets** - JWT secret and connection strings in appsettings (should use Azure Key Vault or User Secrets)
3. **No distributed tracing** - No correlation IDs across services (consider OpenTelemetry)
4. **No resilience patterns** - No retry policies, circuit breakers (consider Polly)
5. **No health checks** - No monitoring endpoints (should add ASP.NET Core Health Checks)
6. **Email logging only** - EmailAPI logs to database but doesn't actually send emails (needs SMTP integration)
7. **No shared contracts** - DTOs duplicated across services (trade-off for microservices independence)
8. **Manual service discovery** - Hardcoded URLs in appsettings (consider Consul or Azure Service Fabric)

### Potential Improvements

1. **Security:**
   - Move secrets to Azure Key Vault
   - Use Azure Managed Identity for Service Bus
   - Implement refresh tokens for JWT
   - Add HTTPS certificate validation
   - Add rate limiting

2. **Resilience:**
   - Add Polly for retry policies and circuit breakers
   - Implement health checks
   - Add correlation IDs for distributed tracing
   - Add structured logging (Serilog)

3. **Architecture:**
   - Implement API Gateway (Ocelot/YARP)
   - Add shared NuGet package for common DTOs
   - Implement CQRS pattern for complex domains
   - Add Redis for distributed caching
   - Implement outbox pattern for reliable messaging

4. **DevOps:**
   - Add Docker support
   - Create Kubernetes manifests
   - Add CI/CD pipelines
   - Implement automated testing (unit, integration, E2E)

## Recent Changes

**Latest commits:**
- `8ecebfc` - Log new user registration (added `loguser` queue consumer)
- `7e1957c` - Service Bus tweaks
- `524bf1f` - EmailAPI model changes
- `52d7289` - Configured AzureServiceBusConsumer and implemented email service

**Modified files (staging area):**
- [Ecommerce.MessageBus/MessageBus.cs](Ecommerce.MessageBus/MessageBus.cs)
- [Ecommerce.Services.EmailAPI/appsettings.json](Ecommerce.Services.EmailAPI/appsettings.json)

## Important Notes for AI Assistants

1. **Always preserve ResponseDto pattern** when creating new endpoints
2. **Never skip automatic migration** (ApplyMigration method must remain)
3. **Follow naming conventions** - ApplicationDbContext, MappingConfig, etc.
4. **Respect service boundaries** - Don't add direct database access across services
5. **Use JWT for inter-service auth** - BackendAPIAuthenticationHttpClientHandler pattern
6. **Queue names must match** between publisher appsettings and consumer appsettings
7. **Service Bus connection strings** - Currently hardcoded, warn user if exposing publicly
8. **Database server name** - Currently "ZAHRAN", may need to update for different environments
9. **Port allocation** - Follow existing pattern (7000-7003), document new ports
10. **Swagger is enabled** - All APIs self-document, use for testing

## References

- **Main branch:** master
- **Git status:** Modified files in MessageBus and EmailAPI (see above)
- **Solution file:** E-commerce.sln
- **Database server:** ZAHRAN (SQL Server local instance)
- **Service Bus:** ecommerceweb.servicebus.windows.net

---

**Last updated:** Auto-generated on initial creation
**Maintained by:** Development team
**Purpose:** Provide context for AI assistants working on this codebase
