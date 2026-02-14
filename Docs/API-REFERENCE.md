# API Endpoints Reference

## AuthAPI (`https://localhost:7002`)

| Method | Endpoint | Description | Auth Required | Role |
|--------|----------|-------------|---------------|------|
| POST | `/api/auth/register` | Register new user | No | - |
| POST | `/api/auth/login` | User login (returns JWT) | No | - |
| POST | `/api/auth/assign-role` | Assign role to user | Yes | ADMIN |

**Request Example (Login):**
```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

**Response Example:**
```json
{
  "result": {
    "user": { "id": "...", "email": "...", "name": "..." },
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  },
  "isSuccess": true,
  "message": ""
}
```

---

## ProductAPI (`https://localhost:7000`)

| Method | Endpoint | Description | Auth Required | Role |
|--------|----------|-------------|---------------|------|
| GET | `/api/product` | List all products | No | - |
| GET | `/api/product/{id}` | Get product by ID | No | - |
| POST | `/api/product` | Create new product | Yes | ADMIN |
| PUT | `/api/product` | Update product | Yes | ADMIN |
| DELETE | `/api/product/{id}` | Delete product | Yes | ADMIN |

**Response Example:**
```json
{
  "result": [
    {
      "productId": 1,
      "name": "iPhone 13",
      "price": 999.99,
      "description": "Latest Apple smartphone",
      "categoryName": "Electronics",
      "imageUrl": "https://..."
    }
  ],
  "isSuccess": true,
  "message": ""
}
```

---

## CouponAPI (`https://localhost:7001`)

| Method | Endpoint | Description | Auth Required | Role |
|--------|----------|-------------|---------------|------|
| GET | `/api/coupon` | List all coupons | No | - |
| GET | `/api/coupon/{id}` | Get coupon by ID | No | - |
| GET | `/api/coupon/GetByCode/{code}` | Get coupon by code | No | - |
| POST | `/api/coupon` | Create coupon | Yes | ADMIN |
| PUT | `/api/coupon` | Update coupon | Yes | ADMIN |
| DELETE | `/api/coupon/{id}` | Delete coupon | Yes | ADMIN |

**Response Example:**
```json
{
  "result": {
    "couponId": 1,
    "couponCode": "10OFF",
    "discountAmount": 10,
    "minAmount": 20
  },
  "isSuccess": true,
  "message": ""
}
```

---

## ShoppingCartAPI (`https://localhost:7003`)

| Method | Endpoint | Description | Auth Required | Role |
|--------|----------|-------------|---------------|------|
| GET | `/api/cart/GetCart/{userId}` | Get user's cart | Yes | Any |
| POST | `/api/cart/CartUpsert` | Add/update cart item | Yes | Any |
| POST | `/api/cart/RemoveCart` | Remove cart item | Yes | Any |
| POST | `/api/cart/ApplyCoupon` | Apply coupon to cart | Yes | Any |
| POST | `/api/cart/RemoveCoupon` | Remove coupon from cart | Yes | Any |
| POST | `/api/cart/EmailCartRequest` | Request cart email | Yes | Any |

**Request Example (CartUpsert):**
```json
{
  "cartHeader": {
    "userId": "user-guid-here",
    "couponCode": ""
  },
  "cartDetails": {
    "productId": 1,
    "count": 2
  }
}
```

---

## EmailAPI (Background Service)

**Consumes Service Bus Queues:**
- `loguser` queue: Processes user registration events
- `emailshoppingcart` queue: Processes cart email requests

**No HTTP endpoints** - runs as a background worker.

---

## Response Format

All APIs return a standardized `ResponseDto`:

```json
{
  "result": <data>,
  "isSuccess": true,
  "message": ""
}
```

On error:
```json
{
  "result": null,
  "isSuccess": false,
  "message": "Error description"
}
```

---

## Swagger Documentation

Each API provides interactive Swagger docs:
- Auth API: https://localhost:7002/swagger
- Product API: https://localhost:7000/swagger
- Coupon API: https://localhost:7001/swagger
- Shopping Cart API: https://localhost:7003/swagger
