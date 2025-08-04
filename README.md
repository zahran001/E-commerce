# E-commerce App

## Project Overview
This is a full-stack e-commerce application built with .NET, following best practices in modular microservices architecture. The solution includes services for authentication, product management, coupon handling, shopping cart, and email notifications. It uses Azure Service Bus for inter-service messaging, implements JWT-based authentication, and supports role-based access (Admin, Customer) using .NET Identity.

## Features
- User registration and authentication (JWT, .NET Identity)
- Role-based access (Admin, Customer)
- Product catalog management
- Shopping cart functionality (with coupon/discount application)
- Coupon management
- Email notifications (via Azure Service Bus)
- Microservices architecture
- API documentation with Swagger/OpenAPI

## Tech Stack
- ASP.NET Core (Web API & MVC)
- Entity Framework Core
- .NET Identity
- JWT Authentication
- AutoMapper
- Azure Service Bus
- SQL Server
- C#
- RESTful APIs
- Swagger/OpenAPI

## Solution Structure
- **E-commerce.Services.AuthAPI**: Authentication, user management, role assignment
- **E-commerce.Services.ProductAPI**: Product catalog CRUD
- **E-commerce.Services.CouponAPI**: Coupon CRUD and discount logic
- **E-commerce.Services.ShoppingCartAPI**: Shopping cart operations, coupon application, email cart requests
- **E-commerce.Web**: MVC frontend, service integration
- **Ecommerce.MessageBus**: Azure Service Bus integration
- **Ecommerce.Services.EmailAPI**: Email notification service (consumes Azure Service Bus)

## Getting Started
1. **Clone the repository**
   ```powershell
   git clone https://github.com/zahran001/E-commerce.git
   ```
2. **Configure the environment**
   - Update connection strings and Azure Service Bus settings in `appsettings.json` for each service.
   - Ensure SQL Server and Azure Service Bus are accessible.
3. **Apply database migrations**
   - Each service applies migrations automatically on startup if needed.
4. **Build the solution**
   ```powershell
   dotnet build E-commerce.sln
   ```

## Running Locally
1. Start each microservice:
   ```powershell
   dotnet run --project E-commerce.Services.AuthAPI/E-commerce.Services.AuthAPI.csproj
   dotnet run --project E-commerce.Services.ProductAPI/E-commerce.Services.ProductAPI.csproj
   dotnet run --project E-commerce.Services.CouponAPI/E-commerce.Services.CouponAPI.csproj
   dotnet run --project E-commerce.Services.ShoppingCartAPI/E-commerce.Services.ShoppingCartAPI.csproj
   dotnet run --project Ecommerce.Services.EmailAPI/Ecommerce.Services.EmailAPI.csproj
   dotnet run --project E-commerce.Web/E-commerce.Web.csproj
   ```
2. Access the web frontend at `http://localhost:5000` (or configured port).
3. API documentation is available at `/swagger` for each service when running in development mode.

## API Endpoints (Summary)

### AuthAPI
- `POST /api/auth/register` — Register new user
- `POST /api/auth/login` — User login
- `POST /api/auth/assign-role` — Assign role to user

### ProductAPI
- `GET /api/product` — List products
- `GET /api/product/{id}` — Get product by ID
- `POST /api/product` — Add product (Admin only)
- `PUT /api/product` — Update product (Admin only)
- `DELETE /api/product/{id}` — Delete product (Admin only)

### CouponAPI
- `GET /api/coupon` — List coupons
- `GET /api/coupon/{id}` — Get coupon by ID
- `GET /api/coupon/GetByCode/{code}` — Get coupon by code
- `POST /api/coupon` — Add coupon (Admin only)
- `PUT /api/coupon` — Update coupon (Admin only)
- `DELETE /api/coupon/{id}` — Delete coupon (Admin only)

### ShoppingCartAPI
- `GET /api/cart/GetCart/{userId}` — Get cart for user
- `POST /api/cart/CartUpsert` — Add/update cart item
- `POST /api/cart/RemoveCart` — Remove cart item
- `POST /api/cart/ApplyCoupon` — Apply coupon to cart
- `POST /api/cart/RemoveCoupon` — Remove coupon from cart
- `POST /api/cart/EmailCartRequest` — Request email for cart

### EmailAPI
- Consumes messages from Azure Service Bus and sends email notifications

> For detailed API documentation, use Swagger UI at `/swagger` for each service.

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.