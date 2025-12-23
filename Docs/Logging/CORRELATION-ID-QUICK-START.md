# Correlation ID - Quick Start Guide

## What Is It?

A **unique ID** that tracks a single user request (like a checkout) across all 6 microservices.

## Why?

**Before correlation IDs**: If something breaks during checkout, you'd have to manually search 6 different services' logs
**After correlation IDs**: Search ONE ID in Seq and see the complete journey

---

## Real Example: User Checkout

### The Request
```
User clicks "Checkout" button at 2:45:30 PM
```

### What Happens Behind the Scenes
```
1. Web MVC generates: CorrelationId = 96ebdbee-45fa-4264-a1b8-c1be5759f40d
2. Web calls ShoppingCartAPI with: X-Correlation-ID: 96ebdbee-...
3. ShoppingCartAPI receives header, uses SAME ID
4. ShoppingCartAPI calls ProductAPI with: X-Correlation-ID: 96ebdbee-...
5. ProductAPI receives header, uses SAME ID
6. ShoppingCartAPI calls CouponAPI with: X-Correlation-ID: 96ebdbee-...
7. CouponAPI receives header, uses SAME ID
8. ShoppingCartAPI publishes to Service Bus with: 96ebdbee-...
9. EmailAPI consumes message with: 96ebdbee-...

Result: ONE ID through all 6 services ✅
```

---

## How to Find a Request in Seq

### Step 1: Get the Correlation ID
From browser response header or logs:
```
X-Correlation-ID: 96ebdbee-45fa-4264-a1b8-c1be5759f40d
```

### Step 2: Open Seq
```
http://localhost:5341
```

### Step 3: Search
Paste the ID in search box:
```
96ebdbee-45fa-4264-a1b8-c1be5759f40d
```

### Step 4: View Complete Journey
You'll see logs from:
- ✅ Web MVC
- ✅ ShoppingCartAPI
- ✅ ProductAPI
- ✅ CouponAPI
- ✅ Service Bus (MessageBus)
- ✅ EmailAPI

**All with timestamps showing exact flow**

---

## Debug Example

### Scenario: Checkout is Slow

**Step 1**: User reports checkout took 5 seconds

**Step 2**: Get correlation ID from request: `96ebdbee-...`

**Step 3**: Search Seq for `96ebdbee-...`

**Step 4**: Look at timestamps:
```
[2:45:30.000] [Web]              Started checkout
[2:45:30.015] [ShoppingCartAPI]  Received request
[2:45:30.032] [ProductAPI]       Fetched in 15ms ✅
[2:45:30.150] [CouponAPI]        Fetched in 2.5s ❌ <- SLOW!
[2:45:30.152] [ShoppingCartAPI]  Sent to Service Bus
[2:45:30.165] [EmailAPI]         Processed in 13ms ✅
```

**Finding**: CouponAPI is slow! 🎯

---

## Different IDs for Different Requests

### This is NORMAL and CORRECT

```
User loads home page:        ID: f15eafd2-... (new request)
User clicks product:         ID: 9d2d769e-... (different request)
User does checkout:          ID: 96ebdbee-... (different request)
```

**Why?** Each button click = separate HTTP request = separate ID

**If you want to trace a specific action**, use that action's ID

---

## Diagnostic Logging

When you run with debugging, you'll see logs like:

```
[MIDDLEWARE] 🆕 POST /Cart/EmailCart - GENERATED NEW ID: 96ebdbee-...
[Web BaseService] ✅ POST https://localhost:7003/... - Added header: 96ebdbee-...
[MIDDLEWARE] ✅ POST /api/cart/EmailCartRequest - Found header: 96ebdbee-...
[ShoppingCart Handler] ✅ GET https://localhost:7000/api/product - Propagating: 96ebdbee-...
[MessageBus] ✅ Using correlation ID from HttpContext: 96ebdbee-...
[EmailAPI] Received message with CorrelationId: 96ebdbee-...
```

**Legend:**
- 🆕 = Generated new ID (first service)
- ✅ = Found and used header
- ❌ = Missing header (indicates problem)
- ⚠️ = Fallback behavior

---

## Architecture

### How It Flows

```
                    User Request (no ID yet)
                            ↓
                    ┌───────────────────┐
                    │   Web MVC         │
                    │   Generates ID    │ ← 🆕 Creates: 96ebdbee-...
                    └────────┬──────────┘
                             │ Add header: X-Correlation-ID
                             ↓
                    ┌───────────────────────────────┐
                    │  ShoppingCartAPI              │
                    │  Receives & uses same ID      │ ← ✅ Uses: 96ebdbee-...
                    └────────┬────────────────┬─────┘
                             │                │
                    Header: 96ebdbee-...     Header: 96ebdbee-...
                             │                │
                             ↓                ↓
                    ┌──────────────┐   ┌──────────────┐
                    │ ProductAPI   │   │ CouponAPI    │
                    │ ✅ Uses: ID  │   │ ✅ Uses: ID  │
                    └──────────────┘   └──────────────┘
                             │
                             ↓ Publish with ID
                    ┌─────────────────────┐
                    │  Service Bus        │
                    │  Message contains   │ ← Message metadata: 96ebdbee-...
                    │  correlation ID     │
                    └──────────┬──────────┘
                               │
                               ↓
                    ┌─────────────────────┐
                    │  EmailAPI Consumer  │
                    │  ✅ Uses: same ID   │ ← Reads from message: 96ebdbee-...
                    └─────────────────────┘

Result: ONE correlation ID tracks complete journey ✅
```

---

## Common Questions

### Q: Why does each page load get a different ID?
**A**: Because each page load is a separate HTTP request from the browser. That's correct! Use the ID from the specific action you want to debug.

### Q: Can I search for all requests from user test1@example.com?
**A**: No, correlation ID is per-request, not per-user-session. But you can search for "test1@example.com" to find their requests, then use each request's ID.

### Q: What if I see different IDs within one request?
**A**: That indicates a propagation failure. Check:
1. Is the header being added by Web? (look for `[Web BaseService] ✅`)
2. Is the API receiving it? (look for `[MIDDLEWARE] ✅` or `[MIDDLEWARE] 🆕`)
3. If you see `[MIDDLEWARE] 🆕`, the header wasn't received - check why

### Q: Can I use this in production?
**A**: Yes! The diagnostic logging only outputs in Debug configuration. In Release/Production, it's disabled.

### Q: What about background jobs not triggered by HTTP?
**A**: The MessageBus has a fallback to generate a new ID if HttpContext isn't available. This is safe.

---

## Files to Know

| File | Purpose |
|------|---------|
| [CorrelationIdMiddleware.cs](E-commerce.Shared/Middleware/CorrelationIdMiddleware.cs) | Generates ID, stores in HttpContext |
| [BaseService.cs](E-commerce.Web/Service/BaseService.cs) | Adds header to Web → API calls |
| [BackendAPIAuthenticationHttpClientHandler.cs](E-commerce.Services.ShoppingCartAPI/Utility/BackendAPIAuthenticationHttpClientHandler.cs) | Adds header to API → API calls |
| [MessageBus.cs](Ecommerce.MessageBus/MessageBus.cs) | Embeds ID in Service Bus messages |

---

## Next Steps

1. **Run your application** with F5
2. **Do a checkout** through the Web UI
3. **Note the correlation ID** from response header or logs
4. **Open Seq**: http://localhost:5341
5. **Search for that ID**
6. **See complete journey** across all services ✅

---

**That's it!** Correlation IDs are now fully integrated and working. Every request is automatically tracked. 🎉
