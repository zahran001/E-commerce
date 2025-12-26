# Phase 4: Azure Deployment - Progress Tracker

**Status:** ✅ Completed
**Started:** 2025-11-15
**Completed:** 2025-11-20
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

- [ ] **NO code changes needed for service URLs**
  - ✅ appsettings.json files stay unchanged with localhost URLs
  - ✅ Environment variables will override at runtime in Azure Container Apps
  - Note: Use `ServiceUrls__ProductAPI=http://productapi` format in Container Apps env vars
  - Rationale: Zero code changes, no file modifications, cleaner deployment

### Service URL Strategy Justification

**Decision: Environment Variables Override (Option 1)** ✅

**Why this approach:**
- ✅ Zero code changes - appsettings.json remains untouched
- ✅ Works with existing local development setup (localhost URLs)
- ✅ Azure Container Apps environment variables override config at runtime
- ✅ No need to create appsettings.Azure.json files
- ✅ Cleaner deployment process

**How it works:**
1. appsettings.json has localhost URLs (local dev)
2. Azure Container Apps sets environment variables like `ServiceUrls__ProductAPI=http://productapi`
3. ASP.NET Core configuration system merges env vars (overrides JSON values)
4. At runtime, services use short names for inter-service communication

**Container Apps Environment Variables to Set:**
```
ServiceUrls__ProductAPI=http://productapi
ServiceUrls__CouponAPI=http://couponapi
ServiceUrls__AuthAPI=http://authapi
ServiceUrls__ShoppingCartAPI=http://shoppingcartapi
```

---

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
**Started:** 2025-11-16
**Completed:** 2025-11-16

#### Tasks:
- [x] Create Azure resource group
  - Resource group name: `Ecommerce-Project`
  - Location: `eastus`
  - Timestamp: 2025-11-16

- [x] Create Azure SQL Server
  - Server name: `ecommerce-sql-server-prod`
  - Admin user: `sqladmin`
  - Timestamp: 2025-11-16

- [x] Create 5 separate databases (Serverless tier)
  - [x] `ecommerce-auth`
  - [x] `ecommerce-product`
  - [x] `ecommerce-coupon`
  - [x] `ecommerce-cart`
  - [x] `ecommerce-email`
  - Strategy: Matches local dev setup, zero code changes required
  - Cost: ~$25/month (5 × $5 serverless)
  - Timestamp: 2025-11-16

- [x] Configure SQL firewall rules
  - Allow Azure services: ✅
  - Allow local IP (for migrations): ✅
  - Timestamp: 2025-11-16

- [x] Create Azure Service Bus namespace
  - Namespace name: `ecommerceweb`
  - SKU: Basic ($10/month)
  - Timestamp: 2025-11-16

- [x] Create Service Bus queues
  - [x] `loguser` queue
  - [x] `emailshoppingcart` queue
  - Timestamp: 2025-11-16

- [x] Create Azure Container Registry
  - Registry name: `ecommerceacr`
  - SKU: Basic ($5/month)
  - Admin enabled: Yes
  - Timestamp: 2025-11-16

- [ ] (Optional) Create Application Insights
  - Name: `ecommerce-insights`
  - Skipped initially: [x]
  - Timestamp: 2025-11-16

---

### Phase 4.2: Secrets Collection
**Estimated time:** 5 minutes
**Started:** 2025-11-16
**Completed:** 2025-11-16

#### Secrets Collected:

**Infrastructure Details:**
- ACR: `ecommerceacr`
- SQL Server: `ecommerce-sql-server-prod`
- Service Bus Namespace: `ecommerceweb`
- Resource Group: `Ecommerce-Project`

**Connection Strings Format:**
```
Server=tcp:ecommerce-sql-server-prod.database.windows.net,1433;Database={DB_NAME};User ID=sqladmin;Password=***;Encrypt=True;TrustServerCertificate=False;
```

- [x] SQL connection strings saved (5 databases)
- [x] Service Bus connection string saved
- [x] JWT secret generated (NEW, not dev secret!)
- [x] ACR credentials saved
- [x] Secrets stored in `.env` file (git ignored)
- Timestamp: 2025-11-16

---

### Phase 4.3: Database Migrations
**Estimated time:** 10 minutes
**Started:** 2025-11-16
**Completed:** 2025-11-16

**CRITICAL:** Run from local machine BEFORE deploying containers!

#### Migration Script Used:
Script: `scripts/run-migrations.ps1`
Command executed: `.\scripts\run-migrations.ps1`

#### Migration Results:
- [x] **AuthAPI** - ✅ Applied successfully
- [x] **ProductAPI** - ✅ Applied successfully
- [x] **CouponAPI** - ✅ Applied successfully
- [x] **ShoppingCartAPI** - ✅ Applied successfully
- [x] **EmailAPI** - ✅ Applied successfully

#### Verification:
- [x] All migrations applied successfully
- [x] Seed data created (Products, Coupons)
- [x] No errors in migration output
- [x] Can connect to Azure SQL databases
- Timestamp: 2025-11-16 (Git commit: `07de42a`)

#### Details:
- All 5 databases created and populated with schema
- Product seed data (4 products) available in `ecommerce-product` database
- Coupon seed data (2 coupons: "10OFF", "20OFF") available in `ecommerce-coupon` database
- User identity tables initialized in `ecommerce-auth` database
- Cart tables initialized in `ecommerce-cart` database
- Email logger table initialized in `ecommerce-email` database

---

### Phase 4.4: Build and Push Docker Images
**Estimated time:** 20 minutes
**Started:** 2025-11-18
**Completed:** 2025-11-20

**⚠️ IMPORTANT:** Update the version number in all commands below whenever building new images. Use semantic versioning (Major.Minor.Patch).

#### Build Instructions:
See [BUILD_AND_DEPLOY.md](../BUILD_AND_DEPLOY.md) for complete step-by-step instructions.

#### Completed Builds:
**Version 1.0.0** - Initial release (2025-11-18)
**Version 1.0.1** - CORS + Auto-migration fixes (2025-11-20)

> **Note for PowerShell users:** Use single-line commands or backticks (`` ` ``) for line continuation. Windows paths use backslashes.

#### Build Summary:

**Version 1.0.1 Images Built & Pushed (2025-11-20):**
- [x] AuthAPI:1.0.1 + latest
- [x] ProductAPI:1.0.1 + latest
- [x] CouponAPI:1.0.1 + latest
- [x] ShoppingCartAPI:1.0.1 + latest
- [x] EmailAPI:1.0.1 + latest
- [x] Web:1.0.1 + latest

**Changes in v1.0.1:**
- ✅ CORS enabled on all 4 APIs (ProductAPI, CouponAPI, AuthAPI, ShoppingCartAPI)
- ✅ Auto-migration disabled in production (all 5 services)
- ✅ JWT settings verified and consistent

**Verification:**
- [x] All 6 images verified in ACR Portal
- [x] Tags `1.0.1` and `latest` pointing to same digest
- Timestamp: 2025-11-20

---

### Phase 4.5: Create Container Apps Environment
**Estimated time:** 5 minutes
**Started:** 2025-11-20
**Completed:** 2025-11-20

```bash
az containerapp env create \
  --name ecommerce-env \
  --resource-group Ecommerce-Project \
  --location eastus \
  --logs-destination none
```

#### Completed:
- [x] Container Apps environment created
- [x] Environment name: `ecommerce-env`
- [x] Resource group: `Ecommerce-Project`
- [x] Location: `eastus`
- [x] Logs destination: `none` (no Log Analytics cost)
- [x] Default domain: `mangosea-a7508352.eastus.azurecontainerapps.io`
- [x] Static IP: `52.226.106.52`
- [x] Workload Profile: `Consumption` (pay-per-use)
- [x] Provisioning State: `Succeeded`

#### Cost Summary:
- Environment baseline: ~$30/month
- 6 services × $0.04/hour each: ~$180/month
- **Total estimated:** ~$210/month

#### Features Enabled (Free/Included):
- ✅ Dapr 1.13.6 (included)
- ✅ KEDA 2.17.2 (included)
- ✅ Internal DNS resolution between services
- ✅ Static public IP
- ✅ Default free domain

#### Features NOT Enabled (Cost Savings):
- ✅ No Log Analytics (saved ~$30-50/month)
- ✅ No Application Insights (saved ~$20-30/month)
- ✅ No custom domain/SSL (can add later)
- ✅ No VNet isolation (can add later)
- ✅ No zone redundancy (single zone)

- Timestamp: 2025-11-20

---

### Phase 4.6: Deploy Services (Tier by Tier)

#### Tier 1: Independent Services
**Estimated time:** 15 minutes
**Started:** 2025-11-20
**Completed:** 2025-11-20

**1. ProductAPI**
- [x] Deployed successfully
- [x] Health check: `https://{fqdn}/health` returns 200
- [x] Internal URL: `productapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io`
- Timestamp: 2025-11-20

**2. CouponAPI**
- [x] Deployed successfully
- [x] Health check: `https://{fqdn}/health` returns 200
- [x] Internal URL: `couponapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io`
- Timestamp: 2025-11-20

**3. AuthAPI**
- [x] Deployed successfully
- [x] Health check: `https://{fqdn}/health` returns 200
- [x] Internal URL: `authapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io`
- Timestamp: 2025-11-20

#### Tier 2: Dependent Services
**Estimated time:** 10 minutes
**Started:** 2025-11-20
**Completed:** 2025-11-20

**4. ShoppingCartAPI**
- [x] Deployed with ProductAPI and CouponAPI URLs
- [x] Health check: `https://{fqdn}/health` returns 200
- [x] Internal URL: `shoppingcartapi.internal.mangosea-a7508352.eastus.azurecontainerapps.io`
- [x] Verified calls to ProductAPI work
- [x] Verified calls to CouponAPI work
- Timestamp: 2025-11-20

#### Tier 3: Background Services
**Estimated time:** 5 minutes
**Started:** 2025-11-20
**Completed:** 2025-11-20

**5. EmailAPI**
- [x] Deployed successfully
- [x] Health check: `https://{fqdn}/health` returns 200
- [x] Logs show Service Bus connection successful
- [x] Timestamp: 2025-11-20

#### Tier 4: Frontend
**Estimated time:** 10 minutes
**Started:** 2025-11-20
**Completed:** 2025-11-20

**6. Web MVC**
- [x] Deployed with external ingress
- [x] Public URL: `web.mangosea-a7508352.eastus.azurecontainerapps.io`
- [x] Can access home page
- [x] Can browse products
- [x] Can register user
- [x] Can login
- [x] Can add to cart
- [x] Timestamp: 2025-11-20

---

## Post-Deployment Verification

### Functional Testing
**Started:** 2025-11-20
**Completed:** 2025-11-20

- [x] **User Registration**
  - Create new user via Web UI
  - Check if email logged in EmailAPI database
  - Timestamp: 2025-11-20

- [x] **User Login**
  - Login with created user
  - Verify JWT token received
  - Timestamp: 2025-11-20

- [x] **Browse Products**
  - View product list
  - Check if products from database display correctly
  - Timestamp: 2025-11-20

- [x] **Apply Coupon**
  - Add product to cart
  - Apply coupon code (e.g., "10OFF")
  - Verify discount applied
  - Timestamp: 2025-11-20

- [x] **Checkout**
  - Complete checkout
  - Check if cart email logged in EmailAPI database
  - Timestamp: 2025-11-20

### Service Bus Testing
- [x] Check `loguser` queue for user registration messages
- [x] Check `emailshoppingcart` queue for cart messages
- [x] Verify EmailAPI consumed messages (check database)
- Timestamp: 2025-11-20

### Performance Testing
- [x] API response times are performing well
- [x] Web page load times responsive
- [x] No timeout errors observed
- Timestamp: 2025-11-20

---

## Monitoring Setup

### Azure Portal Checks
- [x] View Container Apps logs
- [x] Check SQL Database metrics (DTU usage)
- [x] Check Service Bus metrics (message count)
- [x] Review Container Registry usage
- Timestamp: 2025-11-20

### Cost Analysis
- [x] Enable cost alerts (budget: $70/month)
- [x] Review first day's costs
- [x] Projected monthly cost: ~$9/month (cost-optimized with Serverless SQL and Consumption plan)
- Timestamp: 2025-11-20

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

**Key takeaways:**
- Microservices architecture with database-per-service pattern works well in Azure
- Cost optimization is achievable with proper SKU selection (Serverless + Consumption)
- Health checks are critical for container orchestration reliability
- Proper secret management (Key Vault) should be priority for production

---

**Last Updated:** 2025-11-20
**Updated By:** Deployment completion
**Status:** Phase 4 deployment successfully completed
**Next Steps:** CI/CD automation, Key Vault integration, additional monitoring setup
