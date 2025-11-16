# Phase 4: Azure Deployment - Progress Tracker

**Status:** 🟡 In Progress
**Started:** 2025-11-15
**Target Completion:** TBD
**Approach:** MVP - Deploy to Azure Container Apps without NGINX initially

---

## Deployment Strategy Summary

### Design Decisions

| Decision Point | Choice | Rationale |
|----------------|--------|-----------|
| **Application Insights** | ⏳ Skip initially, add post-deployment | Reduce initial complexity, add when observability needed |
| **Service Discovery (FLAW #1)** | ✅ Option B - Azure Container Apps Environment DNS | Services use short names: `http://productapi` |
| **Database Migrations (FLAW #2)** | ✅ Option A + D - Manual migrations + Disable auto-migration | Run migrations ONCE before deployment, prevent race conditions |
| **Secrets Management (FLAW #3)** | ✅ Option A - Bootstrap with Environment Variables | Use env vars initially, migrate to Key Vault later |
| **Service Bus Queues (FLAW #4)** | ✅ Option A - Pre-create in infrastructure phase | Create queues before deploying services |
| **Database Strategy (FLAW #8)** | ✅ Option A - 5 separate databases | Matches local dev setup, zero code changes, true microservices isolation ($25/month) |

---

## Pre-Deployment Checklist

### Code Changes Required

- [ ] **Disable auto-migration in production** (all 5 services)
  - [ ] E-commerce.Services.AuthAPI/Program.cs
  - [ ] E-commerce.Services.ProductAPI/Program.cs
  - [ ] E-commerce.Services.CouponAPI/Program.cs
  - [ ] E-commerce.Services.ShoppingCartAPI/Program.cs
  - [ ] Ecommerce.Services.EmailAPI/Program.cs
  - Script: See `scripts/disable-auto-migration.ps1`

- [ ] **Configure CORS for production** (all APIs)
  - [ ] ProductAPI
  - [ ] CouponAPI
  - [ ] AuthAPI
  - [ ] ShoppingCartAPI
  - Note: Web MVC makes cross-origin calls to APIs

- [ ] **Verify JWT settings are consistent**
  - Issuer: `e-commerce-auth-api`
  - Audience: `e-commerce-client`
  - Secret: Generate NEW 256-bit secret for production

- [ ] **Update service URLs to use short names** (for Container Apps DNS)
  - ShoppingCartAPI: `http://productapi` and `http://couponapi`
  - Web MVC: `http://authapi`, `http://productapi`, `http://couponapi`, `http://shoppingcartapi`
  - Note: Azure Container Apps provides automatic DNS resolution within the same environment

### Database Strategy Justification

**Decision: Use 5 Separate Databases (Option A)** ✅

**Why NOT Option C (Single Database + Schema Separation):**
- ❌ Would break existing local dev setup (uses 5 separate databases)
- ❌ Requires code changes in all ApplicationDbContext files
- ❌ Requires regenerating all EF Core migrations
- ❌ Violates microservices principle (database-per-service)
- ❌ Extra complexity for minimal cost savings ($20/month)

**Why Option A (5 Separate Databases):**
- ✅ Zero code changes - deploy as-is
- ✅ Matches local development environment exactly
- ✅ True microservices isolation
- ✅ Can scale individual databases independently
- ✅ Easier to debug and maintain
- ✅ Can migrate/backup services independently
- ⚠️ Costs $20 more per month ($25 vs $5) - worth it to avoid breaking changes

---

## Deployment Timeline

### Phase 4.1: Infrastructure Setup
**Estimated time:** 20 minutes
**Started:** ___________
**Completed:** ___________

#### Tasks:
- [ ] Create Azure resource group
  - Resource group name: `ecommerce-rg`
  - Location: `eastus`
  - Timestamp: ___________

- [ ] Create Azure SQL Server
  - Server name: `ecommerce-sql-server`
  - Admin user: `sqladmin`
  - Timestamp: ___________

- [ ] Create 5 separate databases (Serverless tier)
  - [ ] `ecommerce-auth`
  - [ ] `ecommerce-product`
  - [ ] `ecommerce-coupon`
  - [ ] `ecommerce-cart`
  - [ ] `ecommerce-email`
  - Strategy: Matches local dev setup, zero code changes required
  - Cost: ~$25/month (5 × $5 serverless)
  - Timestamp: ___________

- [ ] Configure SQL firewall rules
  - Allow Azure services: ✅
  - Allow local IP (for migrations): ___________
  - Timestamp: ___________

- [ ] Create Azure Service Bus namespace
  - Namespace name: `ecommerce-servicebus`
  - SKU: Basic ($10/month)
  - Timestamp: ___________

- [ ] Create Service Bus queues
  - [ ] `loguser` queue
  - [ ] `emailshoppingcart` queue
  - Timestamp: ___________

- [ ] Create Azure Container Registry
  - Registry name: `ecommerceacr`
  - SKU: Basic ($5/month)
  - Admin enabled: Yes
  - Timestamp: ___________

- [ ] (Optional) Create Application Insights
  - Name: `ecommerce-insights`
  - Skipped initially: [ ]
  - Timestamp: ___________

---

### Phase 4.2: Secrets Collection
**Estimated time:** 5 minutes
**Started:** ___________
**Completed:** ___________

#### Secrets to collect:

```bash
# SQL Connection String (format):
# Server=tcp:ecommerce-sql-server.database.windows.net,1433;Database={DB_NAME};User ID=sqladmin;Password={PASSWORD};Encrypt=True;TrustServerCertificate=False;

# If using 5 separate databases:
AUTH_SQL_CONN="Server=tcp:...;Database=ecommerce-auth;..."
PRODUCT_SQL_CONN="Server=tcp:...;Database=ecommerce-product;..."
COUPON_SQL_CONN="Server=tcp:...;Database=ecommerce-coupon;..."
CART_SQL_CONN="Server=tcp:...;Database=ecommerce-cart;..."
EMAIL_SQL_CONN="Server=tcp:...;Database=ecommerce-email;..."

# If using single database:
SQL_CONN="Server=tcp:...;Database=ecommerce-db;..."

# Service Bus Connection String:
SB_CONN="Endpoint=sb://ecommerce-servicebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=..."

# JWT Secret (generate new for production!):
JWT_SECRET="$(openssl rand -base64 64)"
# Do NOT reuse development secret!

# ACR Password:
ACR_PASSWORD="..."
```

- [ ] SQL connection string(s) saved
- [ ] Service Bus connection string saved
- [ ] JWT secret generated (NEW, not dev secret!)
- [ ] ACR credentials saved
- Timestamp: ___________

---

### Phase 4.3: Database Migrations
**Estimated time:** 10 minutes
**Started:** ___________
**Completed:** ___________

**CRITICAL:** Run from local machine BEFORE deploying containers!

#### Migration commands:

```bash
# Update connection string temporarily for migration
# (Use Azure SQL connection string)

cd E-commerce.Services.AuthAPI
dotnet ef database update --connection "{AUTH_SQL_CONN}"
# Timestamp: ___________

cd ../E-commerce.Services.ProductAPI
dotnet ef database update --connection "{PRODUCT_SQL_CONN}"
# Timestamp: ___________

cd ../E-commerce.Services.CouponAPI
dotnet ef database update --connection "{COUPON_SQL_CONN}"
# Timestamp: ___________

cd ../E-commerce.Services.ShoppingCartAPI
dotnet ef database update --connection "{CART_SQL_CONN}"
# Timestamp: ___________

cd ../Ecommerce.Services.EmailAPI
dotnet ef database update --connection "{EMAIL_SQL_CONN}"
# Timestamp: ___________
```

#### Verification:
- [ ] All migrations applied successfully
- [ ] Seed data created (Products, Coupons)
- [ ] No errors in migration output
- [ ] Can connect to Azure SQL from SSMS/Azure Data Studio
- Timestamp: ___________

---

### Phase 4.4: Build and Push Docker Images
**Estimated time:** 20 minutes
**Started:** ___________
**Completed:** ___________

#### Login to ACR:
```bash
az acr login --name ecommerceacr
# Timestamp: ___________
```

#### Build images:
- [ ] AuthAPI
  ```bash
  docker build -t ecommerceacr.azurecr.io/authapi:latest -f E-commerce.Services.AuthAPI/Dockerfile .
  ```
  Timestamp: ___________

- [ ] ProductAPI
  ```bash
  docker build -t ecommerceacr.azurecr.io/productapi:latest -f E-commerce.Services.ProductAPI/Dockerfile .
  ```
  Timestamp: ___________

- [ ] CouponAPI
  ```bash
  docker build -t ecommerceacr.azurecr.io/couponapi:latest -f E-commerce.Services.CouponAPI/Dockerfile .
  ```
  Timestamp: ___________

- [ ] ShoppingCartAPI
  ```bash
  docker build -t ecommerceacr.azurecr.io/shoppingcartapi:latest -f E-commerce.Services.ShoppingCartAPI/Dockerfile .
  ```
  Timestamp: ___________

- [ ] EmailAPI
  ```bash
  docker build -t ecommerceacr.azurecr.io/emailapi:latest -f Ecommerce.Services.EmailAPI/Dockerfile .
  ```
  Timestamp: ___________

- [ ] Web MVC
  ```bash
  docker build -t ecommerceacr.azurecr.io/web:latest -f E-commerce.Web/Dockerfile .
  ```
  Timestamp: ___________

#### Push images:
- [ ] All 6 images pushed to ACR
- [ ] Verified with: `az acr repository list --name ecommerceacr`
- Timestamp: ___________

---

### Phase 4.5: Create Container Apps Environment
**Estimated time:** 5 minutes
**Started:** ___________
**Completed:** ___________

```bash
az extension add --name containerapp --upgrade
az containerapp env create \
  --name ecommerce-env \
  --resource-group ecommerce-rg \
  --location eastus
```

- [ ] Container Apps environment created
- [ ] Environment name: `ecommerce-env`
- Timestamp: ___________

---

### Phase 4.6: Deploy Services (Tier by Tier)

#### Tier 1: Independent Services
**Estimated time:** 15 minutes
**Started:** ___________
**Completed:** ___________

**1. ProductAPI**
- [ ] Deployed successfully
- [ ] Health check: `https://{fqdn}/health` returns 200
- [ ] Internal URL: ___________
- Timestamp: ___________

**2. CouponAPI**
- [ ] Deployed successfully
- [ ] Health check: `https://{fqdn}/health` returns 200
- [ ] Internal URL: ___________
- Timestamp: ___________

**3. AuthAPI**
- [ ] Deployed successfully
- [ ] Health check: `https://{fqdn}/health` returns 200
- [ ] Internal URL: ___________
- Timestamp: ___________

#### Tier 2: Dependent Services
**Estimated time:** 10 minutes
**Started:** ___________
**Completed:** ___________

**4. ShoppingCartAPI**
- [ ] Deployed with ProductAPI and CouponAPI URLs
- [ ] Health check: `https://{fqdn}/health` returns 200
- [ ] Internal URL: ___________
- [ ] Verified calls to ProductAPI work
- [ ] Verified calls to CouponAPI work
- Timestamp: ___________

#### Tier 3: Background Services
**Estimated time:** 5 minutes
**Started:** ___________
**Completed:** ___________

**5. EmailAPI**
- [ ] Deployed successfully
- [ ] Health check: `https://{fqdn}/health` returns 200
- [ ] Logs show Service Bus connection successful
- [ ] Timestamp: ___________

#### Tier 4: Frontend
**Estimated time:** 10 minutes
**Started:** ___________
**Completed:** ___________

**6. Web MVC**
- [ ] Deployed with external ingress
- [ ] Public URL: ___________
- [ ] Can access home page
- [ ] Can browse products
- [ ] Can register user
- [ ] Can login
- [ ] Can add to cart
- [ ] Timestamp: ___________

---

## Post-Deployment Verification

### Functional Testing
**Started:** ___________
**Completed:** ___________

- [ ] **User Registration**
  - Create new user via Web UI
  - Check if email logged in EmailAPI database
  - Timestamp: ___________

- [ ] **User Login**
  - Login with created user
  - Verify JWT token received
  - Timestamp: ___________

- [ ] **Browse Products**
  - View product list
  - Check if products from database display correctly
  - Timestamp: ___________

- [ ] **Apply Coupon**
  - Add product to cart
  - Apply coupon code (e.g., "10OFF")
  - Verify discount applied
  - Timestamp: ___________

- [ ] **Checkout**
  - Complete checkout
  - Check if cart email logged in EmailAPI database
  - Timestamp: ___________

### Service Bus Testing
- [ ] Check `loguser` queue for user registration messages
- [ ] Check `emailshoppingcart` queue for cart messages
- [ ] Verify EmailAPI consumed messages (check database)
- Timestamp: ___________

### Performance Testing
- [ ] API response times < 500ms (p95)
- [ ] Web page load times < 2s
- [ ] No timeout errors
- Timestamp: ___________

---

## Monitoring Setup

### Azure Portal Checks
- [ ] View Container Apps logs
- [ ] Check SQL Database metrics (DTU usage)
- [ ] Check Service Bus metrics (message count)
- [ ] Review Container Registry usage
- Timestamp: ___________

### Cost Analysis
- [ ] Enable cost alerts (budget: $70/month)
- [ ] Review first day's costs
- [ ] Projected monthly cost: $___________
- Timestamp: ___________

---

## Issues Encountered

### Issue Log

| Timestamp | Issue | Service | Resolution | Status |
|-----------|-------|---------|------------|--------|
| | | | | |

---

## Rollback Plan

If deployment fails:

1. **Delete all Container Apps:**
   ```bash
   az containerapp delete --name {app} --resource-group ecommerce-rg
   ```

2. **Database rollback:**
   - Keep database (already paid for serverless min)
   - Delete via: `az sql db delete --name {db} --server ecommerce-sql-server --resource-group ecommerce-rg`

3. **Clean up resources:**
   ```bash
   az group delete --name ecommerce-rg --yes
   ```

---

## Next Steps After Deployment

- [ ] Update GitHub README with live URL
- [ ] Create architecture diagram with actual URLs
- [ ] Write blog post about deployment experience
- [ ] Add Application Insights (Phase 4B)
- [ ] Migrate secrets to Key Vault (Phase 4C)
- [ ] Set up CI/CD pipeline (Phase 6)

---

## Lessons Learned

**What went well:**
-

**Challenges faced:**
-

**Would do differently next time:**
-

**Key takeaways:**
-

---

**Last Updated:** 2025-11-15
**Updated By:** Initial creation
**Next Review:** After first deployment
