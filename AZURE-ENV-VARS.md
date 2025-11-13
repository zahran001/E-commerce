# Azure Container Apps - Environment Variables Reference

**Purpose:** This document lists all environment variables needed for each service when deploying to Azure Container Apps.

**Note:** In Azure, secrets (connection strings, Service Bus, JWT secret) will be retrieved from Azure Key Vault using Managed Identity.

---

## Common Variables (All Services)

```bash
ASPNETCORE_ENVIRONMENT=Production
```

---

## AuthAPI

### Environment Variables

```bash
# Database
ConnectionStrings__DefaultConnection=<from-azure-keyvault>

# JWT Configuration
ApiSettings__JwtOptions__Secret=<from-azure-keyvault>
ApiSettings__JwtOptions__Issuer=e-commerce-auth-api
ApiSettings__JwtOptions__Audience=e-commerce-client

# Service Bus
ServiceBusConnectionString=<from-azure-keyvault>

# Queue Names
TopicAndQueueNames__LogUserQueue=loguser
```

### Key Vault Secrets Referenced
- `SqlConnectionString-Auth` → `ConnectionStrings__DefaultConnection`
- `JwtSecret` → `ApiSettings__JwtOptions__Secret`
- `ServiceBusConnectionString` → `ServiceBusConnectionString`

---

## ProductAPI

### Environment Variables

```bash
# Database
ConnectionStrings__DefaultConnection=<from-azure-keyvault>

# JWT Configuration
ApiSettings__Secret=<from-azure-keyvault>
ApiSettings__Issuer=e-commerce-auth-api
ApiSettings__Audience=e-commerce-client
```

### Key Vault Secrets Referenced
- `SqlConnectionString-Product` → `ConnectionStrings__DefaultConnection`
- `JwtSecret` → `ApiSettings__Secret`

---

## CouponAPI

### Environment Variables

```bash
# Database
ConnectionStrings__DefaultConnection=<from-azure-keyvault>

# JWT Configuration
ApiSettings__Secret=<from-azure-keyvault>
ApiSettings__Issuer=e-commerce-auth-api
ApiSettings__Audience=e-commerce-client
```

### Key Vault Secrets Referenced
- `SqlConnectionString-Coupon` → `ConnectionStrings__DefaultConnection`
- `JwtSecret` → `ApiSettings__Secret`

---

## ShoppingCartAPI

### Environment Variables

```bash
# Database
ConnectionStrings__DefaultConnection=<from-azure-keyvault>

# JWT Configuration
ApiSettings__Secret=<from-azure-keyvault>
ApiSettings__Issuer=e-commerce-auth-api
ApiSettings__Audience=e-commerce-client

# Service-to-Service URLs (Internal Azure Container Apps)
ServiceUrls__ProductAPI=https://productapi.internal.{environment-id}.{region}.azurecontainerapps.io
ServiceUrls__CouponAPI=https://couponapi.internal.{environment-id}.{region}.azurecontainerapps.io

# Service Bus
ServiceBusConnectionString=<from-azure-keyvault>

# Queue Names
TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart
```

### Key Vault Secrets Referenced
- `SqlConnectionString-ShoppingCart` → `ConnectionStrings__DefaultConnection`
- `JwtSecret` → `ApiSettings__Secret`
- `ServiceBusConnectionString` → `ServiceBusConnectionString`

### Notes
- Internal URLs will be determined after Container Apps environment is created
- Format: `https://{app-name}.internal.{env-unique-id}.{region}.azurecontainerapps.io`
- Example: `https://productapi.internal.greenfield-a1b2c3d4.eastus.azurecontainerapps.io`

---

## EmailAPI

### Environment Variables

```bash
# Database
ConnectionStrings__DefaultConnection=<from-azure-keyvault>

# Service Bus
ServiceBusConnectionString=<from-azure-keyvault>

# Queue Names
TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart
TopicAndQueueNames__LogUserQueue=loguser
```

### Key Vault Secrets Referenced
- `SqlConnectionString-Email` → `ConnectionStrings__DefaultConnection`
- `ServiceBusConnectionString` → `ServiceBusConnectionString`

---

## Web MVC

### Environment Variables

```bash
# Service-to-Service URLs (Internal Azure Container Apps)
ServiceUrls__AuthAPI=https://authapi.internal.{environment-id}.{region}.azurecontainerapps.io
ServiceUrls__ProductAPI=https://productapi.internal.{environment-id}.{region}.azurecontainerapps.io
ServiceUrls__CouponAPI=https://couponapi.internal.{environment-id}.{region}.azurecontainerapps.io
ServiceUrls__ShoppingCartAPI=https://shoppingcartapi.internal.{environment-id}.{region}.azurecontainerapps.io
```

### Notes
- No database or secrets needed (stateless frontend)
- All service URLs will be internal Container Apps URLs
- Web MVC will have **external ingress** (public URL)

---

## Azure Key Vault Secrets Summary

**Secrets to create in Key Vault:**

| Secret Name | Value | Used By |
|-------------|-------|---------|
| `ServiceBusConnectionString` | `Endpoint=sb://ecommerceweb...` | AuthAPI, ShoppingCartAPI, EmailAPI |
| `JwtSecret` | 256-bit random key | All APIs |
| `SqlConnectionString-Auth` | Azure SQL connection string | AuthAPI |
| `SqlConnectionString-Product` | Azure SQL connection string | ProductAPI |
| `SqlConnectionString-Coupon` | Azure SQL connection string | CouponAPI |
| `SqlConnectionString-ShoppingCart` | Azure SQL connection string | ShoppingCartAPI |
| `SqlConnectionString-Email` | Azure SQL connection string | EmailAPI |

**Azure SQL Connection String Format:**
```
Server=tcp:ecommerce-sql-server.database.windows.net,1433;Database=ecommerce-db;User ID=sqladmin;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

---

## Container Apps Configuration

### Ingress Settings

| Service | Ingress Type | Target Port | Expose to Internet |
|---------|--------------|-------------|-------------------|
| AuthAPI | Internal | 8080 | No |
| ProductAPI | Internal | 8080 | No |
| CouponAPI | Internal | 8080 | No |
| ShoppingCartAPI | Internal | 8080 | No |
| EmailAPI | Internal | 8080 | No (background service) |
| Web MVC | **External** | 8080 | **Yes** |

### Resource Allocation (MVP)

```bash
# All services (recommended starting point)
--cpu 0.5
--memory 1Gi
--min-replicas 1
--max-replicas 2
```

### Managed Identity Setup

Each Container App will need:
1. **System-assigned Managed Identity** enabled
2. **Key Vault Access Policy** with `Get` and `List` secret permissions

**Azure CLI Example:**
```bash
# Enable managed identity
az containerapp identity assign \
  --name productapi \
  --resource-group ecommerce-rg \
  --system-assigned

# Get principal ID
PRINCIPAL_ID=$(az containerapp identity show \
  --name productapi \
  --resource-group ecommerce-rg \
  --query principalId -o tsv)

# Grant Key Vault access
az keyvault set-policy \
  --name ecommerce-secrets-kv \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

Repeat for all 6 services.

---

## Environment Variable Injection Methods

### Method 1: Azure Portal
1. Go to Container App → **Environment variables**
2. Add each variable with source:
   - **Manual entry** for non-secrets (URLs, queue names)
   - **Key Vault reference** for secrets

### Method 2: Azure CLI
```bash
az containerapp create \
  --name productapi \
  --resource-group ecommerce-rg \
  --environment ecommerce-env \
  --image ecommerceacr.azurecr.io/productapi:latest \
  --target-port 8080 \
  --ingress internal \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    ApiSettings__Issuer=e-commerce-auth-api \
    ApiSettings__Audience=e-commerce-client \
  --secrets \
    sqlconnection=<connection-string> \
    jwtsecret=<jwt-secret>
```

### Method 3: Key Vault Integration (Recommended)
Configure app to read from Key Vault directly using Managed Identity (requires code changes in Phase 4).

---

## Testing Environment Variables

**After deployment, verify variables are set:**

```bash
# Check Container App configuration
az containerapp show \
  --name productapi \
  --resource-group ecommerce-rg \
  --query "properties.template.containers[0].env"

# Check logs for startup errors
az containerapp logs show \
  --name productapi \
  --resource-group ecommerce-rg \
  --follow
```

---

## Troubleshooting

### Service Can't Connect to Database
- ✅ Check `ConnectionStrings__DefaultConnection` is set
- ✅ Verify Azure SQL firewall allows Container Apps
- ✅ Test connection string format (no spaces, proper escaping)

### Service Can't Find Other Services
- ✅ Check `ServiceUrls__*` variables use internal URLs
- ✅ Verify ingress is set to `internal` for API services
- ✅ Confirm all services are in same Container Apps environment

### JWT Authentication Fails
- ✅ Check `ApiSettings__Secret` matches across all services
- ✅ Verify `Issuer` and `Audience` values are consistent
- ✅ Ensure secret is at least 256 bits (32+ characters)

### Service Bus Messages Not Processing
- ✅ Check `ServiceBusConnectionString` is set correctly
- ✅ Verify queue names match (case-sensitive)
- ✅ Check Managed Identity has Service Bus permissions

---

## Phase 4 Checklist

When deploying to Azure, use this checklist:

- [ ] Azure SQL Database created
- [ ] Azure Key Vault created with all secrets
- [ ] Azure Container Apps environment created
- [ ] All 6 Container Apps created
- [ ] Managed identities enabled for all apps
- [ ] Key Vault access policies configured
- [ ] Environment variables set for each app
- [ ] Internal URLs discovered and configured
- [ ] Health checks verified
- [ ] Test end-to-end flow

---

**Last Updated:** Phase 2 Completion
**Next Phase:** Phase 4 - Azure Infrastructure Setup
