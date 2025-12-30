# Azure Redis Caching Deployment Guide

## Overview

This guide provides step-by-step instructions for deploying Redis caching on Azure for your ProductAPI microservice. It covers provisioning Azure Cache for Redis, configuring your application, and deploying to Azure App Service.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Azure Setup](#azure-setup)
3. [Azure Cache for Redis Provisioning](#azure-cache-for-redis-provisioning)
4. [Application Configuration](#application-configuration)
5. [Deployment Steps](#deployment-steps)
6. [Monitoring & Troubleshooting](#monitoring--troubleshooting)
7. [Cost Optimization](#cost-optimization)

---

## Prerequisites

### Required Tools & Accounts

- [x] Azure subscription (with at least $50 credit or pay-as-you-go)
- [x] [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) or Azure Portal access
- [x] [Visual Studio 2022](https://visualstudio.microsoft.com/) or VS Code
- [x] .NET 8 SDK
- [x] [Azure PowerShell](https://learn.microsoft.com/en-us/powershell/azure/install-az-ps) (optional but recommended)

### Verify Installation

```bash
# Check Azure CLI
az --version

# Check .NET SDK
dotnet --version

# Check Azure PowerShell
Get-InstalledModule -Name Az
```

---

## Azure Setup

### Step 1: Login to Azure

```bash
# Login via browser
az login

# Or with specific subscription
az login --subscription YOUR_SUBSCRIPTION_ID

# List all subscriptions
az account list --output table
```

### Step 2: Create Resource Group

A resource group organizes all your Azure resources in one place.

```bash
# Set variables
$resourceGroupName = "ecommerce-rg"
$location = "East US"  # or your preferred region

# Create resource group
az group create \
  --name $resourceGroupName \
  --location $location

# Verify
az group show --name $resourceGroupName
```

**Recommended Regions (for low latency):**
- **East US** - Default, good availability
- **Central US** - Alternative US location
- **West Europe** - For EU users
- **Southeast Asia** - For Asia-Pacific users

---

## Azure Cache for Redis Provisioning

### Step 3: Create Azure Cache for Redis

Choose one of the following approaches:

#### **Option A: Azure Portal (GUI - Easiest)**

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **Create a resource** → Search for **Azure Cache for Redis**
3. Click **Create**
4. Fill in the form:
   - **Resource Group:** ecommerce-rg
   - **Cache Name:** `ecommerce-redis` (must be globally unique)
   - **Location:** East US
   - **Pricing Tier:** Basic (C0 - 250MB) for testing, Standard (C1 - 1GB) for production
   - **Enable non-SSL port:** No (keep SSL for security)
5. Click **Review + Create** → **Create**
6. Wait 10-15 minutes for deployment to complete

#### **Option B: Azure CLI (Faster)**

```bash
# Variables
$resourceGroupName = "ecommerce-rg"
$cacheServiceName = "ecommerce-redis-$(Get-Random)"  # Unique name
$location = "East US"
$skuName = "Basic"
$skuFamily = "C"
$skuCapacity = 0  # 0=250MB (Basic), 1=1GB (Standard), etc.

# Create Redis cache
az redis create \
  --resource-group $resourceGroupName \
  --name $cacheServiceName \
  --location $location \
  --sku $skuName \
  --vm-size $($skuFamily)$skuCapacity \
  --minimum-tls-version 1.2 \
  --enable-non-ssl-port false

# Wait for deployment
Write-Host "Redis cache is being created..."
Start-Sleep -Seconds 30

# Verify creation
az redis show --resource-group $resourceGroupName --name $cacheServiceName
```

### Step 4: Retrieve Redis Connection String

Once Redis is created, get your connection details:

```bash
# Get connection string
az redis list-keys \
  --resource-group $resourceGroupName \
  --name $cacheServiceName

# Get full host info
az redis show \
  --resource-group $resourceGroupName \
  --name $cacheServiceName \
  --query "hostName, sslPort, port"
```

**Expected Output:**
```
HostName: ecommerce-redis-xxxx.redis.cache.windows.net
SslPort: 6380
Port: 6379 (if SSL disabled)
PrimaryKey: [Your-Primary-Key]
SecondaryKey: [Your-Secondary-Key]
```

**Connection String Format:**
```
[Your-Primary-Key]@ecommerce-redis-xxxx.redis.cache.windows.net:6380
```

---

## Application Configuration

### Step 5: Update appsettings Files

#### **Development (appsettings.Development.json)**

Keep using localhost for local development:

```json
{
  "CacheSettings": {
    "Enabled": true,
    "RedisConnection": "localhost:6379",
    "DefaultCacheDuration": 300,
    "SlidingExpiration": true
  }
}
```

#### **Production (appsettings.Production.json)**

Update with Azure Redis connection string:

```json
{
  "CacheSettings": {
    "Enabled": true,
    "RedisConnection": "[PRIMARY-KEY]@ecommerce-redis-xxxx.redis.cache.windows.net:6380,ssl=True,abortConnect=false",
    "DefaultCacheDuration": 3600,
    "SlidingExpiration": true
  }
}
```

**Important Notes:**
- Replace `[PRIMARY-KEY]` with your actual primary key
- Replace `ecommerce-redis-xxxx` with your actual cache name
- Always use `ssl=True` for Azure
- `abortConnect=false` allows graceful degradation if Redis is unavailable

#### **Staging (appsettings.Staging.json)**

```json
{
  "CacheSettings": {
    "Enabled": true,
    "RedisConnection": "[PRIMARY-KEY]@ecommerce-redis-xxxx.redis.cache.windows.net:6380,ssl=True,abortConnect=false",
    "DefaultCacheDuration": 1800,
    "SlidingExpiration": true
  }
}
```

### Step 6: Use Azure Key Vault for Secrets (Best Practice)

Instead of storing connection strings in appsettings, use Azure Key Vault:

#### **Create Key Vault**

```bash
$keyVaultName = "ecommerce-kv-$(Get-Random)"
$resourceGroupName = "ecommerce-rg"
$location = "East US"

# Create Key Vault
az keyvault create \
  --name $keyVaultName \
  --resource-group $resourceGroupName \
  --location $location

# Add Redis connection string secret
az keyvault secret set \
  --vault-name $keyVaultName \
  --name "RedisConnectionString" \
  --value "[PRIMARY-KEY]@ecommerce-redis-xxxx.redis.cache.windows.net:6380,ssl=True,abortConnect=false"

# Verify
az keyvault secret show \
  --vault-name $keyVaultName \
  --name "RedisConnectionString"
```

#### **Update Program.cs to Use Key Vault**

```csharp
// In Program.cs, add this BEFORE builder.Build()

var keyVaultEndpoint = new Uri(builder.Configuration["KeyVault:Endpoint"]);
builder.Configuration.AddAzureKeyVault(
    keyVaultEndpoint,
    new DefaultAzureCredential());

// Now read from Key Vault
var redisConnection = builder.Configuration["RedisConnectionString"];
```

#### **Update appsettings.json**

```json
{
  "KeyVault": {
    "Endpoint": "https://ecommerce-kv-xxxx.vault.azure.net/"
  },
  "CacheSettings": {
    "Enabled": true,
    "RedisConnection": "${RedisConnectionString}",  // Reference from Key Vault
    "DefaultCacheDuration": 3600,
    "SlidingExpiration": true
  }
}
```

---

## Deployment Steps

### Step 7: Prepare Application for Deployment

#### **Create Dockerfile**

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and restore
COPY *.sln .
COPY E-commerce.Services.ProductAPI/ E-commerce.Services.ProductAPI/
RUN dotnet restore

# Build
WORKDIR /app/E-commerce.Services.ProductAPI
RUN dotnet build -c Release

# Publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80
ENTRYPOINT ["dotnet", "E-commerce.Services.ProductAPI.dll"]
```

#### **Create .dockerignore**

```
.git
.gitignore
bin
obj
.vs
.vscode
*.user
*.suo
node_modules
.env
```

### Step 8: Create App Service Plan & App Service

#### **Option A: Azure Portal**

1. Go to Azure Portal
2. Click **Create a resource** → **App Service**
3. Fill in:
   - **Resource Group:** ecommerce-rg
   - **Name:** `ecommerce-productapi` (must be globally unique)
   - **Runtime stack:** .NET 8
   - **Operating System:** Windows (or Linux)
   - **Region:** East US
   - **App Service Plan:** Create new (Pricing: Basic B1 for testing, Standard S1 for production)
4. Click **Review + Create** → **Create**

#### **Option B: Azure CLI**

```bash
$resourceGroupName = "ecommerce-rg"
$appServicePlanName = "ecommerce-plan"
$appServiceName = "ecommerce-productapi-$(Get-Random)"
$location = "East US"
$skuName = "B1"  # Basic tier

# Create App Service Plan
az appservice plan create \
  --name $appServicePlanName \
  --resource-group $resourceGroupName \
  --sku $skuName \
  --is-linux

# Create App Service
az webapp create \
  --resource-group $resourceGroupName \
  --plan $appServicePlanName \
  --name $appServiceName \
  --runtime "DOTNET|8.0"

# Verify
az webapp show \
  --resource-group $resourceGroupName \
  --name $appServiceName
```

### Step 9: Configure App Service Settings

```bash
$resourceGroupName = "ecommerce-rg"
$appServiceName = "ecommerce-productapi-xxxx"

# Set environment variables
az webapp config appsettings set \
  --resource-group $resourceGroupName \
  --name $appServiceName \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    "CacheSettings:Enabled=true" \
    "CacheSettings:RedisConnection=[PRIMARY-KEY]@ecommerce-redis-xxxx.redis.cache.windows.net:6380,ssl=True,abortConnect=false" \
    "CacheSettings:DefaultCacheDuration=3600" \
    "CacheSettings:SlidingExpiration=true"

# Or if using Key Vault, set the Key Vault endpoint
az webapp config appsettings set \
  --resource-group $resourceGroupName \
  --name $appServiceName \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    "KeyVault:Endpoint=https://ecommerce-kv-xxxx.vault.azure.net/"
```

### Step 10: Deploy Application

#### **Option A: Using Visual Studio**

1. Right-click **E-commerce.Services.ProductAPI** → **Publish**
2. Choose **Azure** → **Azure App Service**
3. Select your subscription and app service
4. Click **Publish**
5. VS will build and deploy automatically

#### **Option B: Using Azure CLI with Git**

```bash
# Initialize Git in project (if not already done)
git init
git add .
git commit -m "Initial commit with Redis caching"

# Configure deployment source
az webapp deployment source config-zip \
  --resource-group $resourceGroupName \
  --name $appServiceName \
  --src "path/to/publish.zip"
```

#### **Option C: Using Azure Container Registry (Recommended for Microservices)**

```bash
# Create container registry
$acrName = "ecommerceacr$(Get-Random)"
az acr create \
  --resource-group $resourceGroupName \
  --name $acrName \
  --sku Basic

# Build and push image
az acr build \
  --registry $acrName \
  --image ecommerce-productapi:latest \
  --file E-commerce.Services.ProductAPI/Dockerfile \
  .

# Deploy to App Service
az webapp config container set \
  --name $appServiceName \
  --resource-group $resourceGroupName \
  --docker-custom-image-name $acrName".azurecr.io/ecommerce-productapi:latest" \
  --docker-registry-server-url "https://$acrName.azurecr.io"
```

### Step 11: Configure Network & Security

#### **Add Firewall Rules (Allow App Service to Access Redis)**

```bash
$resourceGroupName = "ecommerce-rg"
$cacheServiceName = "ecommerce-redis-xxxx"
$appServiceName = "ecommerce-productapi-xxxx"

# Get App Service outbound IP
$appServiceIp = az webapp show \
  --resource-group $resourceGroupName \
  --name $appServiceName \
  --query "outboundIpAddresses" -o tsv

# Add to Redis firewall
az redis firewall-rules create \
  --name $cacheServiceName \
  --resource-group $resourceGroupName \
  --rule-name "AllowAppService" \
  --start-ip $appServiceIp \
  --end-ip $appServiceIp
```

#### **Enable Managed Identity for Key Vault Access (Best Practice)**

```bash
# Enable managed identity on App Service
az webapp identity assign \
  --resource-group $resourceGroupName \
  --name $appServiceName

# Get the principal ID
$principalId = az webapp identity show \
  --resource-group $resourceGroupName \
  --name $appServiceName \
  --query "principalId" -o tsv

# Grant Key Vault access
az keyvault set-policy \
  --name $keyVaultName \
  --object-id $principalId \
  --secret-permissions get list
```

---

## Monitoring & Troubleshooting

### Step 12: Enable Monitoring

#### **Application Insights**

```bash
# Create Application Insights
$appInsightsName = "ecommerce-insights"
az monitor app-insights component create \
  --app $appInsightsName \
  --location $location \
  --resource-group $resourceGroupName \
  --application-type web

# Get instrumentation key
$instrumentationKey = az monitor app-insights component show \
  --app $appInsightsName \
  --resource-group $resourceGroupName \
  --query "instrumentationKey" -o tsv

# Add to App Service
az webapp config appsettings set \
  --resource-group $resourceGroupName \
  --name $appServiceName \
  --settings "APPINSIGHTS_INSTRUMENTATIONKEY=$instrumentationKey"
```

#### **Update Program.cs for Application Insights**

```csharp
// Add NuGet package
// dotnet add package Microsoft.ApplicationInsights.AspNetCore

builder.Services.AddApplicationInsightsTelemetry();

// In ConfigureServices
builder.Services.AddApplicationInsightsTelemetry(
    builder.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"]);
```

### Step 13: Monitor Redis Cache

#### **View Redis Metrics in Azure Portal**

1. Go to Azure Portal
2. Navigate to your Redis cache
3. Click **Metrics** in the left sidebar
4. Monitor:
   - **Used Memory** - Cache memory consumption
   - **Cache Hits** - Successful cache lookups
   - **Cache Misses** - Failed cache lookups
   - **Connected Clients** - Active connections
   - **Total Commands Processed** - Request volume

#### **Check Redis Metrics via CLI**

```bash
# Get cache diagnostics
az redis export \
  --resource-group $resourceGroupName \
  --name $cacheServiceName \
  --storage-account mystorageaccount \
  --container-name mycontainer

# View server info
az redis get-properties \
  --resource-group $resourceGroupName \
  --name $cacheServiceName
```

### Step 14: Troubleshooting

#### **Test Redis Connectivity**

```powershell
# Install Redis CLI
choco install redis-64

# Test connection from local machine (if port opened)
redis-cli -h ecommerce-redis-xxxx.redis.cache.windows.net `
          -p 6380 `
          -a [PRIMARY-KEY] `
          ping
```

#### **Common Issues & Solutions**

| Issue | Cause | Solution |
|-------|-------|----------|
| Connection timeout | Redis firewall | Add App Service IP to Redis firewall rules |
| SSL/TLS errors | Certificate validation | Ensure `ssl=True` in connection string |
| Memory exceeded | Cache too small | Upgrade Redis tier or reduce TTL |
| High latency | Network distance | Use Redis in same region as App Service |
| Connection refused | Port blocked | Ensure port 6380 is open (or 6379 for non-SSL) |

#### **View App Service Logs**

```bash
# Stream live logs
az webapp log tail \
  --resource-group $resourceGroupName \
  --name $appServiceName

# Download diagnostic logs
az webapp log download \
  --resource-group $resourceGroupName \
  --name $appServiceName \
  --log-file logs.zip
```

---

## Cost Optimization

### Pricing Estimates (Monthly)

| Component | Tier | Price/Month | Notes |
|-----------|------|------------|-------|
| **Azure Cache for Redis** | Basic (C0) | ~$16 | 250MB cache, for testing |
| | Standard (C1) | ~$68 | 1GB cache, recommended for prod |
| | Premium (P1) | ~$160 | High availability, persistence |
| **App Service Plan** | Basic (B1) | ~$12 | 1 core, 1.75GB RAM |
| | Standard (S1) | ~$74 | 1 core, 1.75GB RAM, better SLA |
| | Premium (P1V2) | ~$150 | 2 cores, 3.5GB RAM |
| **Application Insights** | Pay-as-you-go | ~$2.99/GB ingestion | First 5GB free |
| **Key Vault** | Standard | ~$0.6/month | 10k operations included |

### Cost Saving Tips

1. **Start with Basic tier** - Upgrade only if needed
2. **Use 1-hour TTL** - Reduces cache churn
3. **Implement cache warming** - Reduces cold starts
4. **Monitor hit rates** - Ensure caching is effective
5. **Use Key Vault** - Eliminates storing secrets in code
6. **Enable autoscaling** - Only pay for what you use

### Monitor Costs

```bash
# View Azure Cost Management
az billing subscription list

# Set spending limit
az billing update --allow-az-cli-spending-limit-update true
```

---

## Complete Deployment Checklist

### Pre-Deployment
- [ ] Azure subscription created and verified
- [ ] Azure CLI installed and authenticated
- [ ] Redis implementation code complete and tested locally
- [ ] appsettings files updated for Azure
- [ ] Dockerfile created (if using containers)
- [ ] Security review completed

### Azure Resource Creation
- [ ] Resource group created
- [ ] Azure Cache for Redis provisioned
- [ ] Redis connection string retrieved and noted
- [ ] Key Vault created (if using)
- [ ] Redis connection string stored in Key Vault
- [ ] App Service Plan created
- [ ] App Service created

### Application Configuration
- [ ] Environment variables set on App Service
- [ ] Key Vault endpoint configured
- [ ] Application Insights enabled
- [ ] Managed Identity assigned

### Deployment
- [ ] Application deployed to App Service
- [ ] Deployment verified successfully
- [ ] Application running without errors
- [ ] Health check endpoints responding

### Post-Deployment
- [ ] Monitor metrics in Azure Portal
- [ ] Test cache hit/miss rates
- [ ] Verify database load reduction
- [ ] Check Application Insights logs
- [ ] Document connection strings securely
- [ ] Set up cost alerts

---

## Quick Reference Commands

```bash
# Login
az login

# List all resources
az resource list --output table

# Get Redis connection details
az redis list-keys --resource-group ecommerce-rg --name ecommerce-redis-xxxx

# View App Service URL
az webapp show --resource-group ecommerce-rg --name ecommerce-productapi-xxxx --query defaultHostName

# Restart App Service
az webapp restart --resource-group ecommerce-rg --name ecommerce-productapi-xxxx

# View App Service logs
az webapp log tail --resource-group ecommerce-rg --name ecommerce-productapi-xxxx

# Delete all resources (cleanup)
az group delete --name ecommerce-rg --yes --no-wait
```

---

## Additional Resources

- [Azure Cache for Redis Documentation](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/)
- [App Service Documentation](https://learn.microsoft.com/en-us/azure/app-service/)
- [Azure Key Vault Documentation](https://learn.microsoft.com/en-us/azure/key-vault/)
- [Azure CLI Reference](https://learn.microsoft.com/en-us/cli/azure/reference-index)
- [Application Insights Documentation](https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview)

---

**Document Version:** 1.0
**Created:** 2025-12-29
**Status:** Ready for Implementation
