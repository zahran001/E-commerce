# Phase 4 Implementation Plan - Complete Coverage for All 6 Services

## Overview

This document provides the exact steps to implement OpenTelemetry tracing across all 6 services without gaps:
- **5 API Services**: AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI
- **1 Frontend Service**: Web MVC

**Total Changes**: 12 modifications (2 per service: appsettings.json + Program.cs)

---

## Architecture Reminder

```
E-commerce.Shared/Extensions/OpenTelemetryExtensions.cs
    ↓ (used by all 6 services)
    ├─ AuthAPI
    ├─ ProductAPI
    ├─ CouponAPI
    ├─ ShoppingCartAPI
    ├─ EmailAPI
    └─ Web MVC
    ↓
Jaeger UI (localhost:16686)
```

---

## Implementation Plan

### Phase 4.1: Update appsettings.json in All 6 Services

Each service needs the Jaeger configuration section added. Add this **at the beginning** of the appsettings.json (right after `{`):

#### Template
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
  // ... rest of configuration
}
```

---

### Phase 4.2: Update Program.cs in All 6 Services

For each service, add:
1. **Using statement** at the top
2. **One-line registration** in the services section

#### Template

**Step 1: Add using statement** (after other using statements):
```csharp
using Ecommerce.Shared.Extensions;
```

**Step 2: Add registration** (before `var app = builder.Build();`):
```csharp
builder.Services.AddEcommerceTracing("ServiceName", configuration: builder.Configuration);
```

---

## Detailed Steps Per Service

### 1️⃣ AuthAPI

**Service Name for Tracing**: `"AuthAPI"`

#### Step 1: Update appsettings.json

**File**: `E-commerce.Services.AuthAPI/appsettings.json`

**Current structure** (lines 1-2):
```json
{
  "Serilog": {
```

**Insert after opening brace** (before "Serilog"):
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
  "Serilog": {
```

#### Step 2: Update Program.cs

**File**: `E-commerce.Services.AuthAPI/Program.cs`

**Add using statement** (after line 9, with other using statements):
```csharp
using Ecommerce.Shared.Extensions;
```

**Add registration** (after line 44 `builder.Services.AddSwaggerGen();`, before line 46):
```csharp
// OpenTelemetry Distributed Tracing
builder.Services.AddEcommerceTracing("AuthAPI", configuration: builder.Configuration);
```

**Result**: AuthAPI will trace HTTP requests, database queries, and inter-service calls automatically.

---

### 2️⃣ ProductAPI

**Service Name for Tracing**: `"ProductAPI"`

#### Step 1: Update appsettings.json

**File**: `E-commerce.Services.ProductAPI/appsettings.json`

Add at the very beginning (right after `{`):
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
```

#### Step 2: Update Program.cs

**File**: `E-commerce.Services.ProductAPI/Program.cs`

Add using statement with other imports:
```csharp
using Ecommerce.Shared.Extensions;
```

Add registration before `var app = builder.Build();`:
```csharp
// OpenTelemetry Distributed Tracing
builder.Services.AddEcommerceTracing("ProductAPI", configuration: builder.Configuration);
```

---

### 3️⃣ CouponAPI

**Service Name for Tracing**: `"CouponAPI"`

#### Step 1: Update appsettings.json

**File**: `E-commerce.Services.CouponAPI/appsettings.json`

Add at the very beginning:
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
```

#### Step 2: Update Program.cs

**File**: `E-commerce.Services.CouponAPI/Program.cs`

Add using statement:
```csharp
using Ecommerce.Shared.Extensions;
```

Add registration:
```csharp
// OpenTelemetry Distributed Tracing
builder.Services.AddEcommerceTracing("CouponAPI", configuration: builder.Configuration);
```

---

### 4️⃣ ShoppingCartAPI

**Service Name for Tracing**: `"ShoppingCartAPI"`

#### Step 1: Update appsettings.json

**File**: `E-commerce.Services.ShoppingCartAPI/appsettings.json`

Add at the very beginning:
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
```

#### Step 2: Update Program.cs

**File**: `E-commerce.Services.ShoppingCartAPI/Program.cs`

Add using statement:
```csharp
using Ecommerce.Shared.Extensions;
```

Add registration:
```csharp
// OpenTelemetry Distributed Tracing
builder.Services.AddEcommerceTracing("ShoppingCartAPI", configuration: builder.Configuration);
```

**Important**: This service calls ProductAPI and CouponAPI, so you'll see inter-service call tracing in Jaeger!

---

### 5️⃣ EmailAPI

**Service Name for Tracing**: `"EmailAPI"`

#### Step 1: Update appsettings.json

**File**: `Ecommerce.Services.EmailAPI/appsettings.json`

Add at the very beginning:
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
```

#### Step 2: Update Program.cs

**File**: `Ecommerce.Services.EmailAPI/Program.cs`

Add using statement:
```csharp
using Ecommerce.Shared.Extensions;
```

Add registration:
```csharp
// OpenTelemetry Distributed Tracing
builder.Services.AddEcommerceTracing("EmailAPI", configuration: builder.Configuration);
```

**Important**: This is a background service consumer, so you'll see Service Bus message consumption timing!

---

### 6️⃣ Web MVC

**Service Name for Tracing**: `"Web"`

#### Step 1: Update appsettings.json

**File**: `E-commerce.Web/appsettings.json`

**Current structure** (lines 1-2):
```json
{
  "Logging": {
```

**Insert after opening brace** (before "Logging"):
```json
{
  "Jaeger": {
    "AgentHost": "localhost",
    "AgentPort": 6831
  },
  "Logging": {
```

#### Step 2: Update Program.cs

**File**: `E-commerce.Web/Program.cs`

Add using statement (after line 7, with other using statements):
```csharp
using Ecommerce.Shared.Extensions;
```

Add registration (after line 51 `builder.Services.AddHostedService<DatabaseWarmUpHostedService>();`, before line 53 `var app = builder.Build();`):
```csharp
// OpenTelemetry Distributed Tracing
builder.Services.AddEcommerceTracing("Web", configuration: builder.Configuration);
```

---

## Verification Checklist

After making all changes:

### ✅ Configuration Verification

- [ ] All 6 services have `"Jaeger": { "AgentHost": "localhost", "AgentPort": 6831 }` in appsettings.json
- [ ] All 6 services have `using Ecommerce.Shared.Extensions;` in Program.cs
- [ ] All 6 services have `builder.Services.AddEcommerceTracing("ServiceName", configuration: builder.Configuration);` in Program.cs

### ✅ Build Verification

- [ ] Rebuild solution (Ctrl+Shift+B)
- [ ] No compilation errors
- [ ] All projects build successfully

### ✅ Services and Their Traces

| Service | Service Name | What Gets Traced |
|---------|--------------|------------------|
| AuthAPI | `"AuthAPI"` | Login, registration, JWT generation, DB queries |
| ProductAPI | `"ProductAPI"` | Product CRUD, DB queries, called by ShoppingCart |
| CouponAPI | `"CouponAPI"` | Coupon validation, DB queries, called by ShoppingCart |
| ShoppingCartAPI | `"ShoppingCartAPI"` | Cart operations, calls to Product & Coupon APIs, DB queries, Service Bus publish |
| EmailAPI | `"EmailAPI"` | Service Bus message consumption, DB logging |
| Web MVC | `"Web"` | HTTP requests to all APIs, call timing |

---

## Testing After Implementation

### Step 1: Start Jaeger (if not running)

```powershell
docker run -d --name jaeger `
  -p 6831:6831/udp `
  -p 16686:16686 `
  jaegertracing/all-in-one:latest
```

### Step 2: Set all 6 services as startup projects in Visual Studio

1. Right-click Solution → "Set Startup Projects"
2. Select "Multiple startup projects"
3. Set all 6 to "Start":
   - E-commerce.Services.AuthAPI
   - E-commerce.Services.ProductAPI
   - E-commerce.Services.CouponAPI
   - E-commerce.Services.ShoppingCartAPI
   - Ecommerce.Services.EmailAPI
   - E-commerce.Web
4. Click OK

### Step 3: Press F5 to start all services

Wait for all services to initialize (check Output window).

### Step 4: Perform end-to-end test

1. Open Web MVC: https://localhost:7230
2. Register a new user
3. Log in
4. Browse products
5. Add product to cart (triggers ShoppingCartAPI → ProductAPI/CouponAPI)

### Step 5: View traces in Jaeger

1. Open http://localhost:16686
2. **Service dropdown**: Select `"AuthAPI"` → Find Traces
3. Click on any trace to see waterfall
4. **Expected**: HTTP request → SQL query spans
5. **Try ShoppingCartAPI**: Should see calls to ProductAPI and CouponAPI
6. **Try Web**: Should see HTTP calls to all APIs

### Step 6: Verify correlation ID linking

1. In Jaeger, click on any trace
2. Look for span tags (right panel)
3. Should see `correlation_id` tag from Phase 3!

---

## Success Criteria

Before moving to next phase, verify:

- [ ] All 6 services start without errors
- [ ] Jaeger UI shows traces from all services
- [ ] AuthAPI traces visible (registration, login)
- [ ] ProductAPI traces visible (called by ShoppingCart)
- [ ] CouponAPI traces visible (called by ShoppingCart)
- [ ] ShoppingCartAPI traces show cascading calls to Product/Coupon
- [ ] EmailAPI traces show message consumption
- [ ] Web MVC traces show HTTP calls
- [ ] Correlation IDs appear in Jaeger span tags
- [ ] Waterfall timeline shows accurate timings

---

## Troubleshooting

### Issue: "The type 'AddEcommerceTracing' could not be found"

**Fix**:
1. Verify `using Ecommerce.Shared.Extensions;` is in Program.cs
2. Rebuild solution (Ctrl+Shift+B)
3. Close and reopen Visual Studio if still failing

### Issue: No traces appearing in Jaeger

**Checklist**:
1. Is Jaeger running? `docker ps | grep jaeger`
2. Is http://localhost:16686 accessible?
3. Did you rebuild the solution after adding code?
4. Are services running without errors? (Check Output window)

**Fix**:
```powershell
# Restart Jaeger
docker restart jaeger

# Rebuild in Visual Studio
Ctrl+Shift+B
```

### Issue: "Jaeger server not responding"

**Fix**: Restart Jaeger container
```powershell
docker stop jaeger
docker rm jaeger
docker run -d --name jaeger -p 6831:6831/udp -p 16686:16686 jaegertracing/all-in-one:latest
```

---

## Summary of Changes

**Files Modified**: 12 total (6 services × 2 files each)

| Service | appsettings.json | Program.cs |
|---------|------------------|-----------|
| AuthAPI | Add Jaeger config | Add using + registration |
| ProductAPI | Add Jaeger config | Add using + registration |
| CouponAPI | Add Jaeger config | Add using + registration |
| ShoppingCartAPI | Add Jaeger config | Add using + registration |
| EmailAPI | Add Jaeger config | Add using + registration |
| Web MVC | Add Jaeger config | Add using + registration |

**Lines Added Per Service**:
- appsettings.json: 5 lines (Jaeger section)
- Program.cs: 2 lines (using + 1 registration line)

**Total Lines Added**: ~42 lines across all services

---

## Next Steps

1. ✅ Implement all 12 changes above
2. ✅ Rebuild solution
3. ✅ Run all 6 services
4. ✅ Test with cart checkout flow
5. ✅ Verify traces in Jaeger
6. ✅ Verify correlation ID linking

---

**Document Version**: 1.0
**Created**: 2025-12-24
**Phase**: Phase 4 - OpenTelemetry Tracing Implementation
