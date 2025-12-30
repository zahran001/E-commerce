# Redis Cache Testing Guide

Quick reference for testing the Redis caching implementation in ProductAPI using **Redis CLI** and **Postman**.

## Prerequisites

- **Redis running:** `redis-cli ping` should return `PONG`
- **ProductAPI running** on `https://localhost:7000`
- **Postman** installed
- **Redis CLI** terminal open

---

## Quick Test Summary

| Test | Expected Result | Time |
|------|-----------------|------|
| First GET /api/product | Cache miss, ~10-15ms | 2 min |
| Second GET /api/product | Cache hit, ~2-3ms | 1 min |
| POST new product | Invalidates cache | 3 min |
| PUT product update | Invalidates cache | 3 min |
| DELETE product | Invalidates cache | 3 min |
| Cache expiration (5 min) | TTL expires, forces refresh | 7 min |
| Redis down | Fallback to DB | 3 min |

**Total time: ~22 minutes**

---

## Test 1: Cache Miss → Cache Hit (2 minutes)

### Step 1: First Request (Cache Miss)

**In Postman:**
- `GET http://localhost:7000/api/product`
- Note the response time (should be ~10-15ms)
- Copy the response

**In Redis CLI:**
```bash
KEYS "product:*"
```

Should return empty or only existing keys.

### Step 2: Second Request (Cache Hit)

**In Postman:**
- Send the same `GET http://localhost:7000/api/product` request again
- Response time should be ~2-3ms (significantly faster)
- Response should be identical to first request

**In Redis CLI:**
```bash
# Check cached data
GET "ecommerce_product_product:all"

# Check TTL (time remaining)
TTL "ecommerce_product_product:all"
# Should return ~295-300 seconds
```

**Expected:** You'll see JSON data representing all products in the cache.

---

## Test 2: Get Single Product (1 minute)

### Step 1: First Request

**In Postman:**
- `GET http://localhost:7000/api/product/1`
- Response time: ~8-10ms

### Step 2: Second Request

**In Postman:**
- Send same request again
- Response time: ~1-2ms

**In Redis CLI:**
```bash
# Check the cached single product
GET "ecommerce_product_product:id:1"

# Check TTL
TTL "ecommerce_product_product:id:1"
```

---

## Test 3: Create Product (Invalidates Cache) (3 minutes)

### Step 1: Verify Cache Exists

**In Postman:**
- `GET http://localhost:7000/api/product`
- Response time: ~2ms (should be cached)

**In Redis CLI:**
```bash
KEYS "product:*"
# Should see: ecommerce_product_product:all
```

### Step 2: Create New Product

**In Postman:**
- `POST http://localhost:7000/api/product`
- Body (JSON):
  ```json
  {
    "name": "Test Product",
    "description": "Test Description",
    "price": 99.99,
    "categoryName": "Electronics"
  }
  ```
- Response time: ~15-20ms
- Note the new ProductId

### Step 3: Verify Cache Was Invalidated

**In Postman:**
- `GET http://localhost:7000/api/product`
- Response time: ~10-15ms (cache was cleared, DB query required)
- Should include your new product in the list

### Step 4: Verify Cache Rebuilt

**In Postman:**
- Send same GET request again
- Response time: ~2ms (new cache)

**In Redis CLI:**
```bash
# Check the cache now includes the new product
GET "ecommerce_product_product:all"
```

---

## Test 4: Update Product (Invalidates Cache) (3 minutes)

### Step 1: Verify Cache Exists

**In Postman:**
- `GET http://localhost:7000/api/product/1`
- Response time: ~2ms

### Step 2: Update the Product

**In Postman:**
- `PUT http://localhost:7000/api/product`
- Body (JSON):
  ```json
  {
    "productId": 1,
    "name": "iPhone 15 Pro Max",
    "description": "Updated description",
    "price": 1299.99,
    "categoryName": "Smartphones"
  }
  ```
- Response time: ~10-15ms

### Step 3: Verify Cache Was Invalidated

**In Postman:**
- `GET http://localhost:7000/api/product/1`
- Response time: ~8ms (cache miss, DB query)
- Data should show updated name

**In Redis CLI:**
```bash
# Should show updated name
GET "ecommerce_product_product:id:1"
```

---

## Test 5: Delete Product (Invalidates Cache) (3 minutes)

### Step 1: Delete a Product

**In Postman:**
- `DELETE http://localhost:7000/api/product/1`
- Response should indicate success

### Step 2: Verify Cache Invalidated

**In Postman:**
- `GET http://localhost:7000/api/product`
- Response time: ~10ms (cache cleared, fresh query)
- Product 1 should not be in the list

**In Redis CLI:**
```bash
# Product ID 1 should no longer be cached
GET "ecommerce_product_product:id:1"
# Should return: (nil)
```

---

## Test 6: Cache Expiration (7 minutes)

### Step 1: Check TTL

**In Redis CLI:**
```bash
# Check current TTL (should be close to 300 for dev)
TTL "ecommerce_product_product:all"
# Returns something like: 287
```

### Step 2: Wait for Expiration

**In Redis CLI:**
- Wait ~5 minutes (or watch with MONITOR)
- Check TTL countdown:
  ```bash
  TTL "ecommerce_product_product:all"
  ```
- Eventually returns: `-2` (key expired)

### Step 3: After Expiration

**In Postman:**
- `GET http://localhost:7000/api/product`
- Response time: ~10-15ms (cache miss, DB query)
- Cache rebuilds immediately

**In Redis CLI:**
```bash
# Key should exist again
GET "ecommerce_product_product:all"
```

---

## Test 7: Real-Time Monitoring (Optional)

**In Redis CLI:**
```bash
MONITOR
```

Then in Postman, send requests. You'll see Redis operations in real-time:
```
1. PING (health check)
2. GET "ecommerce_product_product:all" (cache lookup)
3. SET "ecommerce_product_product:all" ... (cache write)
4. DEL "ecommerce_product_product:all" (cache invalidation on write)
```

Exit with `Ctrl+C`

---

## Test 8: Redis Down Scenario (3 minutes)

### Step 1: Stop Redis

**In Redis CLI terminal:**
```bash
# Stop the server
redis-cli shutdown
# Or if running in foreground: Ctrl+C
```

### Step 2: Request Still Works (Fallback)

**In Postman:**
- `GET http://localhost:7000/api/product`
- Should return success (~10-12ms)
- Slower than cached response, but no error
- Falls back to direct database query

Check **Visual Studio Output** for error handling logs.

### Step 3: Restart Redis

```bash
redis-server
# Or use your Redis startup method
```

Verify with:
```bash
redis-cli ping
# Should return: PONG
```

### Step 4: Caching Works Again

**In Postman:**
- `GET http://localhost:7000/api/product`
- Response time: ~2-3ms (caching restored)

---

## Common Redis CLI Commands

```bash
# Health check
ping

# View all cache keys
KEYS "product:*"
KEYS "ecommerce_product_*"

# Get cached value
GET "ecommerce_product_product:all"
GET "ecommerce_product_product:id:1"

# Check time to live
TTL "ecommerce_product_product:all"
# Returns: seconds remaining, or -2 if expired, or -1 if no expiry

# Watch operations in real-time
MONITOR

# Delete specific key (for testing)
DEL "ecommerce_product_product:all"

# Clear entire cache (use with caution!)
FLUSHDB

# Check memory usage
INFO memory

# Get general stats
INFO stats

# Exit
exit
```

---

## Troubleshooting

### "MOVED" or connection error in Redis CLI
```bash
# Verify Redis is running
redis-cli ping
# Should return: PONG

# If not running:
redis-server
```

### Cache not appearing in Redis
- Check ProductAPI logs for errors
- Verify Redis connection string in `appsettings.json` is `localhost:6379`
- Ensure `CacheSettings:Enabled` is `true`

### ProductAPI slow despite cache
- Check TTL might be expired: `TTL "ecommerce_product_product:all"`
- Stop Redis and restart to clear stale connections
- Check database query performance (might be the bottleneck)

### Keys show with `ecommerce_product_` prefix
This is the instance name configured in `Program.cs`. It's normal and helps namespace your cache keys.

---

## Performance Expectations

| Scenario | Expected Time |
|----------|---|
| Cache hit | 1-3ms |
| Cache miss (DB query) | 8-15ms |
| Create/Update/Delete | 10-20ms |
| 10 concurrent cached requests | ~5-10ms total |
| 10 concurrent DB queries | ~50-100ms total |

---

## Logs to Check

In Visual Studio **Output** window, you should see:

**Cache Hit:**
```
Retrieved 4 products from cache
```

**Cache Miss:**
```
Cache miss for all products, querying database
Cached 4 products, cache key: product:all
```

**Cache Invalidation:**
```
Created product 5 and invalidated cache
Updated product 1 and invalidated cache
Deleted product 3 and invalidated cache
```

---

## Next Steps

After testing:
1. Document any performance metrics
2. Check if TTL needs adjustment
3. Verify error handling works
4. Test with other microservices if needed
5. Plan production deployment

