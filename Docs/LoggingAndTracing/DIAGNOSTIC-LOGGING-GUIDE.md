# Diagnostic Logging Guide - Correlation ID Chain Debugging

This guide helps you identify exactly where the correlation ID chain breaks in Test Case 2.

---

## What Was Added

Diagnostic logging has been added to 4 critical components:

1. **CorrelationIdMiddleware.cs** - Shows when IDs are generated vs received
2. **BaseService.cs** (Web) - Shows when headers are added to API calls
3. **BackendAPIAuthenticationHttpClientHandler.cs** - Shows propagation to downstream services
4. **MessageBus.cs** - Shows which correlation ID is published to Service Bus

---

## How to Execute Test Case 2 With Diagnostics

### Step 1: Rebuild Solution

```powershell
# In Visual Studio
Right-click Solution → Clean Solution
Right-click Solution → Rebuild Solution
```

Verify: **Build succeeded** with 0 errors

### Step 2: Open Debug Output Window

```
View → Output Window
```

In the "Show output from" dropdown, select **Debug**

### Step 3: Start All Services

```
Right-click Solution → Set Startup Projects → Multiple startup projects
Set all to "Start": AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, EmailAPI, Web
Press F5
```

Wait for all services to start. You should see in Debug Output:
```
[startup messages from each service]
```

### Step 4: Execute Test Case 2 Flow

1. Open **Web MVC**: https://localhost:7230
2. Click **Login**
3. Enter credentials:
   - Email: `test1@example.com`
   - Password: `Test123!`
4. Click **Login**
5. Click on **iPhone 14** (or any product)
6. Click **Add to Cart**
7. Go to **Cart**
8. Enter coupon code: `10OFF`
9. Click **Apply Coupon**
10. Click **Checkout**

### Step 5: Capture the Logs

As the checkout completes:
1. Look at the **Debug Output** window
2. Copy all lines that start with `[` (the diagnostic logs)
3. Paste them into a text file for analysis

---

## Expected Log Output (If Everything Works)

### Scenario A: ✅ CORRECT (All Same ID)

```
[MIDDLEWARE] 🆕 POST /Cart/Checkout - No header found, GENERATED NEW ID: abc-123-def-456-ghi-789-jkl-012
[Web BaseService] ✅ POST https://localhost:7003/api/cart/checkout - Added X-Correlation-ID header: abc-123-def-456-ghi-789-jkl-012

[MIDDLEWARE] ✅ POST /api/cart/checkout - Found X-Correlation-ID header: abc-123-def-456-ghi-789-jkl-012
[ShoppingCart Handler] ✅ GET https://localhost:7000/api/product/1 - Propagating CorrelationId: abc-123-def-456-ghi-789-jkl-012

[MIDDLEWARE] ✅ GET /api/product/1 - Found X-Correlation-ID header: abc-123-def-456-ghi-789-jkl-012

[ShoppingCart Handler] ✅ GET https://localhost:7001/api/coupon/validate?couponCode=10OFF - Propagating CorrelationId: abc-123-def-456-ghi-789-jkl-012

[MIDDLEWARE] ✅ GET /api/coupon/validate - Found X-Correlation-ID header: abc-123-def-456-ghi-789-jkl-012

[MessageBus] ✅ Using correlation ID from HttpContext: abc-123-def-456-ghi-789-jkl-012
[MessageBus] Publishing to queue 'emailshoppingcart' with CorrelationId: abc-123-def-456-ghi-789-jkl-012
```

**Analysis**:
- ✅ All services have the SAME ID: `abc-123-def-456-ghi-789-jkl-012`
- ✅ Web created it (🆕) and sent it to ShoppingCart
- ✅ ShoppingCart received it (✅) and forwarded to Product/Coupon
- ✅ Product and Coupon received it
- ✅ MessageBus used the same ID for Service Bus message
- **Result**: Correlation ID chain is WORKING ✅

---

## Failure Scenarios & How to Debug Them

### Scenario B: ❌ BROKEN AT SHOPPING CART (Different ID Generated)

**Log Pattern**:
```
[MIDDLEWARE] 🆕 POST /Cart/Checkout - No header found, GENERATED NEW ID: abc-123
[Web BaseService] ✅ POST https://localhost:7003/api/cart/checkout - Added X-Correlation-ID header: abc-123

[MIDDLEWARE] 🆕 POST /api/cart/checkout - No header found, GENERATED NEW ID: xyz-789
[ShoppingCart Handler] ❌ GET https://localhost:7000/api/product/1 - No CorrelationId found in HttpContext.Items - header NOT added!

[MIDDLEWARE] 🆕 GET /api/product/1 - No header found, GENERATED NEW ID: def-456
```

**What This Means**:
- Web created `abc-123` ✅
- Web sent it to ShoppingCartAPI ✅
- **But ShoppingCartAPI didn't receive or read the header** ❌
- ShoppingCartAPI generated new ID: `xyz-789` ❌
- ShoppingCartAPI couldn't propagate ID to Product ❌
- Product generated its own ID: `def-456` ❌

**Root Causes to Check**:

1. **Is X-Correlation-ID header actually being sent from Web?**
   - The log shows `[Web BaseService] ✅` so it IS being added
   - **But was the header actually sent in the HTTP request?**
   - Check: Network tab in browser DevTools (F12 → Network)
   - Look for the request to ShoppingCartAPI
   - Check if `X-Correlation-ID` header appears in Request Headers

2. **Is ShoppingCartAPI's middleware receiving the request?**
   - The log shows `[MIDDLEWARE] 🆕 ... GENERATED NEW ID`
   - This means the header check FAILED: `TryGetValue("X-Correlation-ID", ...)`
   - Check: Is the header name exactly `X-Correlation-ID`? (case-sensitive in some cases)
   - Check: Is something stripping headers from the request?

3. **Is middleware running in the correct order in ShoppingCartAPI?**
   - Open [E-commerce.Services.ShoppingCartAPI/Program.cs:116](E-commerce.Services.ShoppingCartAPI/Program.cs#L116)
   - Verify: `app.UseCorrelationId();` is AFTER `app.UseCors()` and BEFORE `app.UseAuthentication()`
   - If in wrong position, CORS or auth might interfere with header reading

**Fix Steps**:
- Verify header name is exactly `X-Correlation-ID` (not `X-CorrelationId` or `correlationid`)
- Verify middleware order in all Program.cs files
- Check if CORS policy is blocking custom headers (add to allowed headers if needed)
- Check if proxy or load balancer is stripping the header

---

### Scenario C: ❌ BROKEN AT MESSAGE BUS (ID Lost)

**Log Pattern**:
```
[MIDDLEWARE] ✅ POST /api/cart/checkout - Found X-Correlation-ID header: abc-123
[ShoppingCart Handler] ✅ GET https://localhost:7000/api/product/1 - Propagating CorrelationId: abc-123
[MIDDLEWARE] ✅ GET /api/product/1 - Found X-Correlation-ID header: abc-123

[MessageBus] ⚠️  Both HttpContext and Activity null - FALLBACK: Generated new GUID: def-456
[MessageBus] Publishing to queue 'emailshoppingcart' with CorrelationId: def-456
```

**What This Means**:
- HTTP calls are working correctly ✅
- All services from Web → Product → Coupon have same ID: `abc-123` ✅
- **But MessageBus couldn't read the ID from ShoppingCartAPI's HttpContext** ❌
- MessageBus fell back to generating new ID: `def-456` ❌
- EmailAPI gets different ID than the request ❌

**Root Causes to Check**:

1. **Is HttpContext null when MessageBus.PublishMessage is called?**
   - The log shows `⚠️  Both HttpContext and Activity null`
   - This means `_httpContextAccessor.HttpContext` is null
   - Check: Is MessageBus being called from a controller action?
   - If yes, HttpContext should be available

2. **Is IHttpContextAccessor properly registered in ShoppingCartAPI?**
   - Open [E-commerce.Services.ShoppingCartAPI/Program.cs:47](E-commerce.Services.ShoppingCartAPI/Program.cs#L47)
   - Verify: `builder.Services.AddHttpContextAccessor();` exists
   - If missing, add this line

3. **Is IHttpContextAccessor properly injected in MessageBus?**
   - Open [Ecommerce.MessageBus/MessageBus.cs:19](Ecommerce.MessageBus/MessageBus.cs#L19)
   - Verify: Constructor has `IHttpContextAccessor httpContextAccessor` parameter
   - Verify: Field `_httpContextAccessor` stores it
   - If missing, add these

**Fix Steps**:
```csharp
// In ShoppingCartAPI/Program.cs (before MessageBus registration)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IMessageBus, MessageBus>();
```

---

### Scenario D: ❌ WEB MIDDLEWARE ISSUE (No ID in Web MVC)

**Log Pattern**:
```
[Web BaseService] ❌ POST https://localhost:7003/api/cart/checkout - No CorrelationId found in HttpContext.Items - header NOT added!

[MIDDLEWARE] 🆕 POST /api/cart/checkout - No header found, GENERATED NEW ID: xyz-789
```

**What This Means**:
- Web's middleware didn't create correlation ID in HttpContext.Items ❌
- BaseService couldn't find the ID to propagate ❌
- ShoppingCartAPI generated its own ID ❌

**Root Causes to Check**:

1. **Is CorrelationIdMiddleware registered in Web's Program.cs?**
   - Open [E-commerce.Web/Program.cs:71](E-commerce.Web/Program.cs#L71)
   - Verify: `app.UseCorrelationId();` exists
   - If missing, add it

2. **Is middleware registered in correct order?**
   - Verify: `app.UseCorrelationId();` is BEFORE `app.UseAuthentication()` and `app.UseAuthorization()`
   - Verify: It's AFTER `app.UseStaticFiles()` and `app.UseRouting()`
   - Correct order: Routing → **Correlation ID** → Authentication → Authorization

3. **Is E-commerce.Shared project referenced?**
   - Right-click Web project → Properties
   - Verify: E-commerce.Shared appears in Project References
   - If missing, add reference

**Fix Steps**:
```csharp
// In Web/Program.cs
app.UseRouting();
app.UseCorrelationId();  // ← Add if missing
app.UseAuthentication();
app.UseAuthorization();
```

---

## Log Analysis Checklist

When you capture the logs, use this checklist to diagnose:

### ✅ Checkpoints (Each should show "✅ or 🆕")

- [ ] **Web creates ID**: `[MIDDLEWARE] 🆕 POST /Cart/Checkout - ... GENERATED NEW ID: <ID1>`
- [ ] **Web sends ID**: `[Web BaseService] ✅ POST https://localhost:7003/... Added X-Correlation-ID header: <ID1>`
- [ ] **ShoppingCart receives ID**: `[MIDDLEWARE] ✅ POST /api/cart/checkout - Found X-Correlation-ID header: <ID1>`
- [ ] **ShoppingCart sends to Product**: `[ShoppingCart Handler] ✅ GET https://localhost:7000/... Propagating CorrelationId: <ID1>`
- [ ] **Product receives ID**: `[MIDDLEWARE] ✅ GET /api/product/1 - Found X-Correlation-ID header: <ID1>`
- [ ] **ShoppingCart sends to Coupon**: `[ShoppingCart Handler] ✅ GET https://localhost:7001/... Propagating CorrelationId: <ID1>`
- [ ] **Coupon receives ID**: `[MIDDLEWARE] ✅ GET /api/coupon/validate - Found X-Correlation-ID header: <ID1>`
- [ ] **MessageBus uses ID**: `[MessageBus] ✅ Using correlation ID from HttpContext: <ID1>`
- [ ] **Same ID throughout**: All `<ID1>` values are identical

### ❌ Red Flags (Indicate problems)

- [ ] **"🆕 No header found"** at ShoppingCart, Product, or Coupon = Header not propagating
- [ ] **"❌ No CorrelationId found"** = ShoppingCart can't propagate downstream
- [ ] **"⚠️ Both HttpContext and Activity null"** = MessageBus context lost
- [ ] **Different IDs between services** = Chain broken at that point

---

## Next Steps Based on What You Find

### If All Checkpoints Pass (Scenario A) ✅
- **Correlation ID implementation is WORKING**
- The issue might be in how Seq is displaying the data, or in other parts
- Run the complete test and verify Seq shows same ID across all services

### If Checkpoint Fails at ShoppingCart (Scenario B) ❌
1. Check header name consistency
2. Verify middleware order in ShoppingCartAPI/Program.cs
3. Check CORS configuration
4. Enable network tracing to verify header is actually sent

### If Checkpoint Fails at MessageBus (Scenario C) ❌
1. Verify IHttpContextAccessor is registered in ShoppingCartAPI
2. Verify IHttpContextAccessor is injected in MessageBus constructor
3. Check if MessageBus is called from a non-HTTP context (background job)

### If Checkpoint Fails at Web (Scenario D) ❌
1. Verify middleware registration in Web/Program.cs
2. Verify E-commerce.Shared is referenced by Web
3. Check middleware order in Web pipeline

---

## Summary

**Run Test Case 2** → **Capture Debug Output** → **Compare against Scenarios A-D** → **Apply fixes based on which scenario matches**

This diagnostic approach will identify the exact point where correlation ID is lost and guide you to the fix.

---

## Additional Tips

### Enable Verbose Logging in Visual Studio
```
Tools → Options → Debugging → Output Window
Set "Diagnostic Tools" logging to maximum
```

### View Raw HTTP Traffic
```
Browser F12 → Network tab
Click the request to ShoppingCartAPI
Check "Request headers" and "Response headers"
Verify X-Correlation-ID appears
```

### Check Seq in Real-Time
```
Open Seq: http://localhost:5341
Run Test Case 2
Watch for new logs appearing
Search by the first few characters of the ID to find all related logs
```

---
