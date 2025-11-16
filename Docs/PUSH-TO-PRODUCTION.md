# Push to Production - Quick Guide

**Target:** Azure Container Apps
**Estimated Time:** 1-2 hours (first deployment)
**Monthly Cost:** ~$60-70

---

## Key Deployment Decisions

| Decision | Choice | Impact |
|----------|--------|--------|
| **Database Strategy** | 5 Separate Databases | Zero code changes, matches local dev |
| **Service Discovery** | Azure Container Apps DNS | Use short names: `http://productapi` |
| **Auto-Migration** | Disabled in Production | Manual migrations before deployment |
| **Secrets** | Environment Variables | Migrate to Key Vault later (optional) |
| **Application Insights** | Skip initially | Add post-deployment if needed |

---

## Prerequisites

- [ ] Azure subscription active
- [ ] Azure CLI installed (`az --version`)
- [ ] Docker Desktop running
- [ ] Logged into Azure (`az login`)
- [ ] All code changes committed to Git

---

## Part 1: Code Preparation (15 minutes)

### Step 1.1: Disable Auto-Migration in Production

Run this script to update all services:

```powershell
# See: scripts/disable-auto-migration.ps1
.\scripts\disable-auto-migration.ps1
```

Or manually update each service's `Program.cs`:

```csharp
// Find ApplyMigration() method and wrap in environment check:
void ApplyMigration()
{
    if (!app.Environment.IsProduction())  // ADD THIS LINE
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
}
```

**Services to update:**
- E-commerce.Services.AuthAPI/Program.cs
- E-commerce.Services.ProductAPI/Program.cs
- E-commerce.Services.CouponAPI/Program.cs
- E-commerce.Services.ShoppingCartAPI/Program.cs
- Ecommerce.Services.EmailAPI/Program.cs

### Step 1.2: Configure CORS for Production

Add to each API's `Program.cs` (before `var app = builder.Build();`):

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

Add after `var app = builder.Build();`:

```csharp
app.UseCors("AllowAll");
```

### Step 1.3: Commit Changes

```bash
git add .
git commit -m "Prepare for Azure deployment: disable auto-migration, configure CORS"
git push
```

---

## Part 2: Azure Infrastructure (20 minutes)

### Step 2.1: Set Variables

```bash
RESOURCE_GROUP="ecommerce-rg"
LOCATION="eastus"
SQL_SERVER="ecommerce-sql-$(date +%s)"  # Add timestamp for uniqueness
SQL_ADMIN="sqladmin"
SQL_PASSWORD="YourStrong!Password$(openssl rand -base64 6)"  # Generate secure password
SERVICEBUS_NS="ecommerce-sb-$(date +%s)"
ACR_NAME="ecommerceacr$(date +%s | tail -c 5)"  # Last 4 digits of timestamp
```

### Step 2.2: Create Resource Group

```bash
az group create --name $RESOURCE_GROUP --location $LOCATION
```

### Step 2.3: Create SQL Server and Databases

**Strategy: 5 Separate Databases (Matches Local Dev Setup)**

This approach uses 5 separate Azure SQL databases, one per microservice. This matches your local development setup exactly and requires zero code changes.

```bash
# Create SQL Server
az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $SQL_ADMIN \
  --admin-password "$SQL_PASSWORD"

# Allow Azure services
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Allow your local IP (for migrations)
MY_IP=$(curl -s ifconfig.me)
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowMyIP \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP

# Create 5 databases (Serverless tier)
for db in auth product coupon cart email; do
  az sql db create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER \
    --name ecommerce-$db \
    --edition GeneralPurpose \
    --compute-model Serverless \
    --family Gen5 \
    --capacity 1 \
    --auto-pause-delay 60
done
```

### Step 2.4: Create Service Bus and Queues

```bash
# Create namespace
az servicebus namespace create \
  --name $SERVICEBUS_NS \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Basic

# Create queues
az servicebus queue create \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $SERVICEBUS_NS \
  --name loguser

az servicebus queue create \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $SERVICEBUS_NS \
  --name emailshoppingcart
```

### Step 2.5: Create Container Registry

```bash
az acr create \
  --name $ACR_NAME \
  --resource-group $RESOURCE_GROUP \
  --sku Basic \
  --admin-enabled true
```

### Step 2.6: Collect Secrets

```bash
# Generate NEW JWT secret for production
JWT_SECRET=$(openssl rand -base64 64)

# Get Service Bus connection string
SB_CONN=$(az servicebus namespace authorization-rule keys list \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $SERVICEBUS_NS \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv)

# Get ACR credentials
ACR_PASSWORD=$(az acr credential show \
  --name $ACR_NAME \
  --query 'passwords[0].value' -o tsv)

# Build connection strings
AUTH_SQL="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=ecommerce-auth;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False;"
PRODUCT_SQL="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=ecommerce-product;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False;"
COUPON_SQL="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=ecommerce-coupon;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False;"
CART_SQL="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=ecommerce-cart;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False;"
EMAIL_SQL="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=ecommerce-email;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False;"

# Save secrets to file (for reference)
cat > azure-secrets.txt <<EOF
SQL_SERVER=$SQL_SERVER
SQL_PASSWORD=$SQL_PASSWORD
JWT_SECRET=$JWT_SECRET
SB_CONN=$SB_CONN
ACR_NAME=$ACR_NAME
ACR_PASSWORD=$ACR_PASSWORD
EOF

echo "⚠️  IMPORTANT: Save azure-secrets.txt securely and DO NOT commit to Git!"
```

---

## Part 3: Database Migrations (10 minutes)

**CRITICAL:** Run BEFORE deploying containers!

```bash
# Navigate to each service and run migrations
cd E-commerce.Services.AuthAPI
dotnet ef database update --connection "$AUTH_SQL"

cd ../E-commerce.Services.ProductAPI
dotnet ef database update --connection "$PRODUCT_SQL"

cd ../E-commerce.Services.CouponAPI
dotnet ef database update --connection "$COUPON_SQL"

cd ../E-commerce.Services.ShoppingCartAPI
dotnet ef database update --connection "$CART_SQL"

cd ../Ecommerce.Services.EmailAPI
dotnet ef database update --connection "$EMAIL_SQL"

cd ..
```

**Verify:** Check Azure portal → SQL databases → Query editor → See tables created

---

## Part 4: Build and Push Images (20 minutes)

```bash
# Login to ACR
az acr login --name $ACR_NAME

# Build all images
docker build -t $ACR_NAME.azurecr.io/authapi:latest \
  -f E-commerce.Services.AuthAPI/Dockerfile .

docker build -t $ACR_NAME.azurecr.io/productapi:latest \
  -f E-commerce.Services.ProductAPI/Dockerfile .

docker build -t $ACR_NAME.azurecr.io/couponapi:latest \
  -f E-commerce.Services.CouponAPI/Dockerfile .

docker build -t $ACR_NAME.azurecr.io/shoppingcartapi:latest \
  -f E-commerce.Services.ShoppingCartAPI/Dockerfile .

docker build -t $ACR_NAME.azurecr.io/emailapi:latest \
  -f Ecommerce.Services.EmailAPI/Dockerfile .

docker build -t $ACR_NAME.azurecr.io/web:latest \
  -f E-commerce.Web/Dockerfile .

# Push all images
docker push $ACR_NAME.azurecr.io/authapi:latest
docker push $ACR_NAME.azurecr.io/productapi:latest
docker push $ACR_NAME.azurecr.io/couponapi:latest
docker push $ACR_NAME.azurecr.io/shoppingcartapi:latest
docker push $ACR_NAME.azurecr.io/emailapi:latest
docker push $ACR_NAME.azurecr.io/web:latest

# Verify
az acr repository list --name $ACR_NAME --output table
```

---

## Part 5: Deploy to Azure Container Apps (30 minutes)

### Step 5.1: Create Container Apps Environment

```bash
az extension add --name containerapp --upgrade

az containerapp env create \
  --name ecommerce-env \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION
```

### Step 5.2: Deploy Tier 1 Services (Independent)

**ProductAPI:**

```bash
az containerapp create \
  --name productapi \
  --resource-group $RESOURCE_GROUP \
  --environment ecommerce-env \
  --image $ACR_NAME.azurecr.io/productapi:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --registry-username $ACR_NAME \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress internal \
  --min-replicas 1 \
  --max-replicas 1 \
  --cpu 0.5 \
  --memory 1Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=$PRODUCT_SQL" \
    "ApiSettings__Secret=$JWT_SECRET" \
    "ApiSettings__Issuer=e-commerce-auth-api" \
    "ApiSettings__Audience=e-commerce-client"
```

**CouponAPI:**

```bash
az containerapp create \
  --name couponapi \
  --resource-group $RESOURCE_GROUP \
  --environment ecommerce-env \
  --image $ACR_NAME.azurecr.io/couponapi:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --registry-username $ACR_NAME \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress internal \
  --min-replicas 1 \
  --max-replicas 1 \
  --cpu 0.5 \
  --memory 1Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=$COUPON_SQL" \
    "ApiSettings__Secret=$JWT_SECRET" \
    "ApiSettings__Issuer=e-commerce-auth-api" \
    "ApiSettings__Audience=e-commerce-client"
```

**AuthAPI:**

```bash
az containerapp create \
  --name authapi \
  --resource-group $RESOURCE_GROUP \
  --environment ecommerce-env \
  --image $ACR_NAME.azurecr.io/authapi:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --registry-username $ACR_NAME \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress internal \
  --min-replicas 1 \
  --max-replicas 1 \
  --cpu 0.5 \
  --memory 1Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=$AUTH_SQL" \
    "ServiceBusConnectionString=$SB_CONN" \
    "TopicAndQueueNames__LogUserQueue=loguser" \
    "ApiSettings__JwtOptions__Secret=$JWT_SECRET" \
    "ApiSettings__JwtOptions__Issuer=e-commerce-auth-api" \
    "ApiSettings__JwtOptions__Audience=e-commerce-client"
```

### Step 5.3: Deploy Tier 2 Services (Dependent)

**ShoppingCartAPI:**

```bash
az containerapp create \
  --name shoppingcartapi \
  --resource-group $RESOURCE_GROUP \
  --environment ecommerce-env \
  --image $ACR_NAME.azurecr.io/shoppingcartapi:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --registry-username $ACR_NAME \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress internal \
  --min-replicas 1 \
  --max-replicas 1 \
  --cpu 0.5 \
  --memory 1Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=$CART_SQL" \
    "ServiceBusConnectionString=$SB_CONN" \
    "TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart" \
    "ServiceUrls__ProductAPI=http://productapi" \
    "ServiceUrls__CouponAPI=http://couponapi" \
    "ApiSettings__Secret=$JWT_SECRET" \
    "ApiSettings__Issuer=e-commerce-auth-api" \
    "ApiSettings__Audience=e-commerce-client"
```

### Step 5.4: Deploy Tier 3 (Background Services)

**EmailAPI:**

```bash
az containerapp create \
  --name emailapi \
  --resource-group $RESOURCE_GROUP \
  --environment ecommerce-env \
  --image $ACR_NAME.azurecr.io/emailapi:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --registry-username $ACR_NAME \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress internal \
  --min-replicas 1 \
  --max-replicas 1 \
  --cpu 0.25 \
  --memory 0.5Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=$EMAIL_SQL" \
    "ServiceBusConnectionString=$SB_CONN" \
    "TopicAndQueueNames__EmailShoppingCartQueue=emailshoppingcart" \
    "TopicAndQueueNames__LogUserQueue=loguser"
```

### Step 5.5: Deploy Tier 4 (Frontend)

**Web MVC:**

```bash
az containerapp create \
  --name web \
  --resource-group $RESOURCE_GROUP \
  --environment ecommerce-env \
  --image $ACR_NAME.azurecr.io/web:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --registry-username $ACR_NAME \
  --registry-password "$ACR_PASSWORD" \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 2 \
  --cpu 0.5 \
  --memory 1Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ServiceUrls__AuthAPI=http://authapi" \
    "ServiceUrls__ProductAPI=http://productapi" \
    "ServiceUrls__CouponAPI=http://couponapi" \
    "ServiceUrls__ShoppingCartAPI=http://shoppingcartapi"
```

---

## Part 6: Get Public URL and Test

```bash
# Get public URL
WEB_URL=$(az containerapp show \
  --name web \
  --resource-group $RESOURCE_GROUP \
  --query properties.configuration.ingress.fqdn -o tsv)

echo ""
echo "🎉 DEPLOYMENT COMPLETE!"
echo "🌐 Public URL: https://$WEB_URL"
echo ""
echo "Test the application:"
echo "  1. Open: https://$WEB_URL"
echo "  2. Register a new user"
echo "  3. Browse products"
echo "  4. Add to cart"
echo "  5. Apply coupon (10OFF or 20OFF)"
echo "  6. Complete checkout"
echo ""
```

---

## Part 7: Verification Checklist

- [ ] Web UI loads successfully
- [ ] Can register new user
- [ ] Can login
- [ ] Products display correctly
- [ ] Can add items to cart
- [ ] Coupon discount applies
- [ ] Checkout completes
- [ ] Check EmailAPI database for logged emails
- [ ] All health endpoints return 200:
  - `https://productapi-{env}.azurecontainerapps.io/health`
  - `https://couponapi-{env}.azurecontainerapps.io/health`
  - `https://authapi-{env}.azurecontainerapps.io/health`
  - `https://shoppingcartapi-{env}.azurecontainerapps.io/health`
  - `https://emailapi-{env}.azurecontainerapps.io/health`

---

## Troubleshooting

### Container won't start

```bash
# Check logs
az containerapp logs show \
  --name {service-name} \
  --resource-group $RESOURCE_GROUP \
  --follow
```

Common issues:
- Missing environment variable
- Wrong connection string format
- Database not migrated
- ACR authentication failed

### Service Bus messages not processing

```bash
# Check EmailAPI logs
az containerapp logs show --name emailapi --resource-group $RESOURCE_GROUP

# Check queue metrics in Azure portal
```

### CORS errors

- Ensure `app.UseCors("AllowAll")` is AFTER `app.UseRouting()` and BEFORE `app.UseAuthorization()`

---

## Rollback

If deployment fails catastrophically:

```bash
# Delete entire resource group (removes everything)
az group delete --name $RESOURCE_GROUP --yes --no-wait

# Start over from Part 2
```

---

## Next Steps

1. **Update GitHub README** with live URL
2. **Monitor costs** in Azure Cost Management
3. **Set up alerts** for downtime
4. **Add Application Insights** (optional)
5. **Migrate to Key Vault** for secrets (optional)
6. **Set up CI/CD** with GitHub Actions (Phase 6)

---

## Cost Breakdown (Estimated)

| Resource | Monthly Cost |
|----------|--------------|
| Container Apps (6 apps) | ~$30 |
| SQL Databases (5 × Serverless) | ~$25 |
| Service Bus (Basic) | $10 |
| Container Registry (Basic) | $5 |
| **Total** | **~$70** |

**Why 5 separate databases?**
- ✅ Matches local development setup (zero code changes)
- ✅ True microservices isolation (database-per-service)
- ✅ No need to regenerate migrations or modify DbContext
- ⚠️ Costs $20 more than single database, but saves hours of refactoring

**Cost optimization:**
- Serverless SQL auto-pauses after 1 hour idle (reduces cost when not in use)
- Container Apps can scale to zero (configure if needed)
- Basic tier for non-critical resources (Service Bus, ACR)

---

**Last Updated:** 2025-11-15
**Status:** Ready for deployment
