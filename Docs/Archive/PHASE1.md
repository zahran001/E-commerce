# Phase 1: Security Hardening - Step-by-Step Guide

**Status:** 🔴 Not Started
**Estimated Time:** 30-45 minutes
**Goal:** Remove all hardcoded secrets from source control and configure User Secrets for local development

---

## ⚠️ CRITICAL: Backup Before Starting

```bash
# Create a backup branch (just in case)
git checkout -b backup-before-phase1
git checkout master  # or your main branch
```

**Why?** If something breaks, you can easily revert:
```bash
git checkout backup-before-phase1
```

---

## 📋 Overview

This phase will:
1. ✅ Set up User Secrets for all 5 services (automated via script)
2. ✅ Remove hardcoded secrets from `appsettings.json` files
3. ✅ Update `MessageBus.cs` to read Service Bus connection from configuration
4. ✅ Verify everything still works locally
5. ✅ Commit cleaned-up code (no secrets in Git)

**What changes:** Configuration values move from `appsettings.json` → User Secrets
**What stays the same:** Database names, server names, service URLs (your local dev setup)

---

## Step 1: Run User Secrets Setup Script

### 1.1 Verify Prerequisites

**Check you're in the right location:**
```bash
# Open PowerShell in your E-commerce root folder
cd c:\Users\minha\source\repos\E-commerce
pwd
# Should output: C:\Users\minha\source\repos\E-commerce
```

**Verify script exists:**
```bash
ls scripts\setup-user-secrets.ps1
# Should show the file with today's date
```

### 1.2 Run the Script

```bash
.\scripts\setup-user-secrets.ps1
```

**Expected Output:**
```
===========================================================
  E-commerce User Secrets Setup
===========================================================

Configuration:
  SQL Server: ZAHRAN
  JWT Secret Length: 58 characters
  Service Bus: Configured

Configuring E-commerce.Services.AuthAPI...
  → Initializing user secrets...
  → Setting connection string (DB: E-commerce_Auth)
  → Setting JWT secret (ApiSettings:JwtOptions:Secret)
  → Setting TopicAndQueueNames:LogUserQueue = loguser
  ✅ E-commerce.Services.AuthAPI configured successfully

Configuring E-commerce.Services.ProductAPI...
  → Initializing user secrets...
  → Setting connection string (DB: E-commerce_Product)
  → Setting JWT secret (ApiSettings:Secret)
  → Setting ApiSettings:Issuer = e-commerce-auth-api
  → Setting ApiSettings:Audience = e-commerce-client
  ✅ E-commerce.Services.ProductAPI configured successfully

Configuring E-commerce.Services.CouponAPI...
  → Initializing user secrets...
  → Setting connection string (DB: E-commerce_Coupon)
  → Setting JWT secret (ApiSettings:Secret)
  → Setting ApiSettings:Issuer = e-commerce-auth-api
  → Setting ApiSettings:Audience = e-commerce-client
  ✅ E-commerce.Services.CouponAPI configured successfully

Configuring E-commerce.Services.ShoppingCartAPI...
  → Initializing user secrets...
  → Setting connection string (DB: E-commerce_ShoppingCart)
  → Setting JWT secret (ApiSettings:Secret)
  → Setting Service Bus connection
  → Setting ApiSettings:Issuer = e-commerce-auth-api
  → Setting ApiSettings:Audience = e-commerce-client
  → Setting ServiceUrls:ProductAPI = https://localhost:7000
  → Setting ServiceUrls:CouponAPI = https://localhost:7001
  → Setting TopicAndQueueNames:EmailShoppingCartQueue = emailshoppingcart
  ✅ E-commerce.Services.ShoppingCartAPI configured successfully

Configuring Ecommerce.Services.EmailAPI...
  → Initializing user secrets...
  → Setting connection string (DB: E-commerce_Email)
  → Setting Service Bus connection
  → Setting TopicAndQueueNames:EmailShoppingCartQueue = emailshoppingcart
  → Setting TopicAndQueueNames:LogUserQueue = loguser
  ✅ Ecommerce.Services.EmailAPI configured successfully

===========================================================
  Verification
===========================================================

Checking E-commerce.Services.AuthAPI...
  ✅ 3 secrets configured
     ConnectionStrings:DefaultConnection = Server=ZAHRAN;Database=E-commerce_Auth;...
     ApiSettings:JwtOptions:Secret = local-dev-secret-min-32-chars-abcdefghijklm...
     TopicAndQueueNames:LogUserQueue = loguser

... (similar for other services) ...

===========================================================
  Summary
===========================================================

Services configured successfully: 5

✅ Setup Complete!

All services are configured for local development.
Secrets are stored in: C:\Users\minha\AppData\Roaming\Microsoft\UserSecrets\
```

### 1.3 Verification Checkpoint ✅

**Check the output:**
- [ ] All 5 services show ✅ configured successfully
- [ ] Verification section shows secrets for each service
- [ ] Summary shows "Services configured successfully: 5"
- [ ] No error messages in red

**If you see errors:**
```bash
# Re-run the script (it's safe to run multiple times)
.\scripts\setup-user-secrets.ps1
```

**Manual verification (optional):**
```bash
# Check secrets for one service
cd E-commerce.Services.ProductAPI
dotnet user-secrets list

# Expected output:
# ConnectionStrings:DefaultConnection = Server=ZAHRAN;Database=E-commerce_Product;...
# ApiSettings:Secret = local-dev-secret-min-32-chars-abcdefghijklmnopqrstuvwxyz123456
# ApiSettings:Issuer = e-commerce-auth-api
# ApiSettings:Audience = e-commerce-client

cd ..
```

**✅ CHECKPOINT: Do NOT proceed until script runs successfully with 5/5 services configured**

---

## Step 2: Update MessageBus.cs to Use Configuration

### 2.1 Open the File

```
File: Ecommerce.MessageBus\MessageBus.cs
```

### 2.2 Current Code (Lines 11-13)

```csharp
public class MessageBus : IMessageBus
{
    private string connectionString = "";
```

### 2.3 Replace With

```csharp
using Microsoft.Extensions.Configuration;  // ← Add this at top of file (around line 3)

// ... existing usings ...

public class MessageBus : IMessageBus
{
    private readonly string connectionString;

    public MessageBus(IConfiguration configuration)
    {
        connectionString = configuration["ServiceBusConnectionString"]
            ?? throw new ArgumentNullException(nameof(configuration), "ServiceBusConnectionString not configured");
    }
```

### 2.4 Complete Updated File

**Full content of MessageBus.cs should look like:**

```csharp
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;  // ← NEW
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.MessageBus
{
    public class MessageBus : IMessageBus
    {
        private readonly string connectionString;  // ← CHANGED: added readonly

        // ← NEW: Constructor accepting IConfiguration
        public MessageBus(IConfiguration configuration)
        {
            connectionString = configuration["ServiceBusConnectionString"]
                ?? throw new ArgumentNullException(nameof(configuration), "ServiceBusConnectionString not configured");
        }

        // publish message to service bus
        public async Task PublishMessage(object message, string topic_queue_name)
        {
            await using var client = new ServiceBusClient(connectionString);

            ServiceBusSender sender = client.CreateSender(topic_queue_name);

            var jsonMessage = JsonConvert.SerializeObject(message);
            ServiceBusMessage finalMessage = new ServiceBusMessage(Encoding
                .UTF8.GetBytes(jsonMessage))
            {
                CorrelationId = Guid.NewGuid().ToString(),
            };

            await sender.SendMessageAsync(finalMessage);
            await client.DisposeAsync();
        }
    }
}
```

### 2.5 Verification Checkpoint ✅

**Build the MessageBus project:**
```bash
cd Ecommerce.MessageBus
dotnet build
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**If you get errors:**
- Check you added `using Microsoft.Extensions.Configuration;` at the top
- Check the constructor syntax matches exactly
- Make sure `connectionString` is now `readonly`

**✅ CHECKPOINT: MessageBus.dll builds without errors**

```bash
cd ..  # Back to root
```

---

## Step 3: Clean Up appsettings.json Files

### 3.1 AuthAPI - appsettings.json

**File:** `E-commerce.Services.AuthAPI\appsettings.json`

**Current:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=ZAHRAN;Database=E-commerce_Auth;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "ApiSettings": {
    "JwtOptions": {
      "Secret": "A ninja must always be prepared Scooby Dooby Doo VROOM VROOM Pumpkin Muncher",
      "Issuer": "e-commerce-auth-api",
      "Audience": "e-commerce-client"
    }
  },
  "TopicAndQueueNames": {
    "LogUserQueue": "loguser"
  }
}
```

**Change to:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "ApiSettings": {
    "JwtOptions": {
      "Secret": "",
      "Issuer": "e-commerce-auth-api",
      "Audience": "e-commerce-client"
    }
  },
  "TopicAndQueueNames": {
    "LogUserQueue": "loguser"
  }
}
```

**Changes:**
- Line 10: `"DefaultConnection": ""` ← Empty (comes from User Secrets)
- Line 14: `"Secret": ""` ← Empty (comes from User Secrets)
- Lines 15-16: Keep Issuer and Audience (not secrets)
- Lines 19-20: Keep queue names (not secrets)

---

### 3.2 ProductAPI - appsettings.json

**File:** `E-commerce.Services.ProductAPI\appsettings.json`

**Current:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=ZAHRAN;Database=E-commerce_Product;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "ApiSettings": {
    "Secret": "A ninja must always be prepared Scooby Dooby Doo VROOM VROOM Pumpkin Muncher",
    "Issuer": "e-commerce-auth-api",
    "Audience": "e-commerce-client"
  }
}
```

**Change to:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "ApiSettings": {
    "Secret": "",
    "Issuer": "e-commerce-auth-api",
    "Audience": "e-commerce-client"
  }
}
```

**Changes:**
- Line 10: `"DefaultConnection": ""` ← Empty
- Line 13: `"Secret": ""` ← Empty
- Lines 14-15: Keep Issuer and Audience

---

### 3.3 CouponAPI - appsettings.json

**File:** `E-commerce.Services.CouponAPI\appsettings.json`

**Same pattern as ProductAPI** - empty out:
- `ConnectionStrings:DefaultConnection`
- `ApiSettings:Secret`

---

### 3.4 ShoppingCartAPI - appsettings.json

**File:** `E-commerce.Services.ShoppingCartAPI\appsettings.json`

**Current:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=ZAHRAN;Database=E-commerce_ShoppingCart;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "ApiSettings": {
    "Secret": "A ninja must always be prepared Scooby Dooby Doo VROOM VROOM Pumpkin Muncher",
    "Issuer": "e-commerce-auth-api",
    "Audience": "e-commerce-client"
  },
  "ServiceUrls": {
    "ProductAPI": "https://localhost:7000",
    "CouponAPI": "https://localhost:7001"
  },
  "TopicAndQueueNames": {
    "EmailShoppingCartQueue": "emailshoppingcart"
  }
}
```

**Change to:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "ApiSettings": {
    "Secret": "",
    "Issuer": "e-commerce-auth-api",
    "Audience": "e-commerce-client"
  },
  "ServiceUrls": {
    "ProductAPI": "https://localhost:7000",
    "CouponAPI": "https://localhost:7001"
  },
  "TopicAndQueueNames": {
    "EmailShoppingCartQueue": "emailshoppingcart"
  }
}
```

**Changes:**
- Line 10: `"DefaultConnection": ""` ← Empty
- Line 13: `"Secret": ""` ← Empty
- Lines 17-22: Keep ServiceUrls and queue names (not secrets)

---

### 3.5 EmailAPI - appsettings.json

**File:** `Ecommerce.Services.EmailAPI\appsettings.json`

**Current:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=ZAHRAN;Database=E-commerce_Email;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "ServiceBusConnectionString": "",
  "TopicAndQueueNames": {
    "EmailShoppingCartQueue": "emailshoppingcart",
    "LogUserQueue": "loguser"
  }
}
```

**Change to:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "ServiceBusConnectionString": "",
  "TopicAndQueueNames": {
    "EmailShoppingCartQueue": "emailshoppingcart",
    "LogUserQueue": "loguser"
  }
}
```

**Changes:**
- Line 10: `"DefaultConnection": ""` ← Empty
- Line 12: `"ServiceBusConnectionString": ""` ← Empty
- Lines 13-16: Keep queue names (not secrets)

---

### 3.6 Verification Checkpoint ✅

**Check git status:**
```bash
git status
```

**Expected output:**
```
modified:   E-commerce.Services.AuthAPI/appsettings.json
modified:   E-commerce.Services.CouponAPI/appsettings.json
modified:   E-commerce.Services.ProductAPI/appsettings.json
modified:   E-commerce.Services.ShoppingCartAPI/appsettings.json
modified:   Ecommerce.MessageBus/MessageBus.cs
modified:   Ecommerce.Services.EmailAPI/appsettings.json
```

**Verify no secrets visible:**
```bash
git diff E-commerce.Services.AuthAPI/appsettings.json
```

Should show lines with secrets being **removed** (showing as `-` lines in red).

**✅ CHECKPOINT: All appsettings.json files cleaned, no secrets in git diff**

---

## Step 4: Test Each Service Individually

### 4.1 Test ProductAPI (No dependencies)

```bash
cd E-commerce.Services.ProductAPI
dotnet run
```

**Expected output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5187
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shutdown.
```

**Open browser:** https://localhost:7000/swagger

**Verify:**
- [ ] Swagger UI loads
- [ ] GET /api/product endpoint shows products
- [ ] No errors in console

**Stop the service:** Press `Ctrl+C`

```bash
cd ..  # Back to root
```

---

### 4.2 Test CouponAPI (No dependencies)

```bash
cd E-commerce.Services.CouponAPI
dotnet run
```

**Expected:** Starts on https://localhost:7001/swagger

**Verify:**
- [ ] Swagger UI loads
- [ ] GET /api/coupon endpoint works
- [ ] No errors in console

**Stop:** `Ctrl+C`

```bash
cd ..
```

---

### 4.3 Test AuthAPI (No dependencies, publishes to Service Bus)

```bash
cd E-commerce.Services.AuthAPI
dotnet run
```

**Expected:** Starts on https://localhost:7002/swagger

**Verify:**
- [ ] Swagger UI loads
- [ ] Service starts without Service Bus errors
- [ ] No errors in console

**Optional advanced test (register a user):**
1. POST /api/auth/register with a test user
2. Should succeed (doesn't throw Service Bus error)
3. Check EmailAPI would receive the message (test in Step 4.5)

**Stop:** `Ctrl+C`

```bash
cd ..
```

---

### 4.4 Test ShoppingCartAPI (Calls ProductAPI + CouponAPI, publishes to Service Bus)

**IMPORTANT: Keep ProductAPI and CouponAPI running in separate terminals**

**Terminal 1:**
```bash
cd E-commerce.Services.ProductAPI
dotnet run
# Keep running
```

**Terminal 2:**
```bash
cd E-commerce.Services.CouponAPI
dotnet run
# Keep running
```

**Terminal 3 (new):**
```bash
cd E-commerce.Services.ShoppingCartAPI
dotnet run
```

**Expected:** Starts on https://localhost:7003/swagger

**Verify:**
- [ ] Swagger UI loads
- [ ] Service starts without errors
- [ ] No "failed to connect to ProductAPI/CouponAPI" errors
- [ ] Service Bus connection works

**Stop all 3 terminals:** `Ctrl+C` in each

```bash
cd ..
```

---

### 4.5 Test EmailAPI (Consumes from Service Bus)

```bash
cd Ecommerce.Services.EmailAPI
dotnet run
```

**Expected output:**
```
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shutdown.
info: Ecommerce.Services.EmailAPI.Services.AzureServiceBusConsumer[0]
      Starting Azure Service Bus consumers...
```

**Verify:**
- [ ] Service starts without errors
- [ ] No "failed to connect to Service Bus" errors
- [ ] Consumers start successfully

**Stop:** `Ctrl+C`

```bash
cd ..
```

---

### 4.6 Complete Integration Test (All Services)

**Start all 5 services in separate terminals:**

**Terminal 1:** AuthAPI
```bash
cd E-commerce.Services.AuthAPI && dotnet run
```

**Terminal 2:** ProductAPI
```bash
cd E-commerce.Services.ProductAPI && dotnet run
```

**Terminal 3:** CouponAPI
```bash
cd E-commerce.Services.CouponAPI && dotnet run
```

**Terminal 4:** ShoppingCartAPI
```bash
cd E-commerce.Services.ShoppingCartAPI && dotnet run
```

**Terminal 5:** EmailAPI
```bash
cd Ecommerce.Services.EmailAPI && dotnet run
```

**Verify:**
- [ ] All 5 services start without errors
- [ ] No "configuration not found" errors
- [ ] No "connection string missing" errors

**Test flow (via Swagger or Postman):**

1. **Register a user** (AuthAPI POST /api/auth/register)
   - Should trigger message to `loguser` queue
   - EmailAPI should log the registration

2. **Login** (AuthAPI POST /api/auth/login)
   - Should return JWT token

3. **Get products** (ProductAPI GET /api/product)
   - Should return product list

4. **Add to cart** (ShoppingCartAPI POST /api/cart)
   - Use JWT token in Authorization header
   - Should create cart

5. **Checkout cart** (ShoppingCartAPI POST /api/cart/EmailCartRequest)
   - Should trigger message to `emailshoppingcart` queue
   - EmailAPI should log the cart email

**✅ CHECKPOINT: All services run together, Service Bus messages flow correctly**

**Stop all services:** `Ctrl+C` in each terminal

---

## Step 5: Verify No Secrets in Git

### 5.1 Check Git Status

```bash
git status
```

**Should show:**
```
modified:   E-commerce.Services.AuthAPI/appsettings.json
modified:   E-commerce.Services.CouponAPI/appsettings.json
modified:   E-commerce.Services.ProductAPI/appsettings.json
modified:   E-commerce.Services.ShoppingCartAPI/appsettings.json
modified:   Ecommerce.MessageBus/MessageBus.cs
modified:   Ecommerce.Services.EmailAPI/appsettings.json
Untracked files:
  scripts/setup-user-secrets.ps1
  PHASE1.md
```

### 5.2 Search for Exposed Secrets

```bash
# Search for Service Bus connection string
git diff | findstr /C:"SharedAccessKey"
# Should return NOTHING (or only show lines being REMOVED with -)

# Search for JWT secret
git diff | findstr /C:"ninja"
# Should return NOTHING (or only show lines being REMOVED with -)
```

**If you see secrets in `+` lines (green/additions):**
- ❌ You missed cleaning a file
- Go back to Step 3 and empty out that value

**If you only see secrets in `-` lines (red/deletions):**
- ✅ Good! Git is removing secrets, not adding them

### 5.3 Verification Checkpoint ✅

- [ ] `git diff` shows secrets being removed (red `-` lines)
- [ ] No secrets in added lines (green `+` lines)
- [ ] All connection strings and secrets show as `""`

**✅ CHECKPOINT: No secrets will be committed to Git**

---

## Step 6: Update .gitignore (Safety Net)

### 6.1 Open .gitignore

**File:** `.gitignore` (in root folder)

### 6.2 Add These Lines (if not already present)

```gitignore
# User Secrets
appsettings.Production.json
appsettings.*.json
!appsettings.json
!appsettings.Development.json

# Personal notes
SECRETS.txt
*.secrets.txt
```

**Explanation:**
- `appsettings.*.json` ignores all variant files
- `!appsettings.json` allows base file (not ignored)
- `!appsettings.Development.json` allows dev file (not ignored)
- This prevents accidentally committing production configs later

### 6.3 Verification

```bash
git status
```

Should **not** show any `appsettings.Production.json` files (even if you create them later).

---

## Step 7: Commit Changes

### 7.1 Stage Files

```bash
git add .
```

### 7.2 Review What Will Be Committed

```bash
git status
```

**Should show:**
```
Changes to be committed:
  modified:   .gitignore
  modified:   E-commerce.Services.AuthAPI/appsettings.json
  modified:   E-commerce.Services.CouponAPI/appsettings.json
  modified:   E-commerce.Services.ProductAPI/appsettings.json
  modified:   E-commerce.Services.ShoppingCartAPI/appsettings.json
  modified:   Ecommerce.MessageBus/MessageBus.cs
  modified:   Ecommerce.Services.EmailAPI/appsettings.json
  new file:   PHASE1.md
  new file:   scripts/setup-user-secrets.ps1
```

### 7.3 Final Safety Check

```bash
# View exactly what will be committed
git diff --staged

# Make sure NO secrets are visible in the diff
# All connection strings and secrets should show as ""
```

### 7.4 Commit

```bash
git commit -m "Phase 1: Security hardening - remove hardcoded secrets

- Implement User Secrets for local development (all 5 services)
- Update MessageBus.cs to accept configuration via IConfiguration
- Remove hardcoded Service Bus connection string
- Remove hardcoded JWT secrets from appsettings.json
- Remove hardcoded SQL connection strings from appsettings.json
- Add setup script for easy developer onboarding
- Update .gitignore to prevent future secret leaks

Local development now uses User Secrets (not in Git).
Production will use Azure Key Vault (Phase 4)."
```

### 7.5 Verification Checkpoint ✅

```bash
# View the commit
git log -1 --stat
```

**Verify:**
- [ ] Commit created successfully
- [ ] 8-9 files changed
- [ ] Commit message describes changes

**Push to remote (optional, but recommended):**
```bash
git push origin master  # or your branch name
```

**✅ CHECKPOINT: Changes committed, secrets not in Git history**

---

## 🎉 Phase 1 Complete!

### What You Accomplished

✅ **Security:**
- No secrets in source control
- User Secrets configured for local dev
- Service Bus connection string externalized
- JWT secrets externalized
- SQL connection strings externalized

✅ **Development:**
- All 5 services run locally (verified)
- Service Bus messaging works
- Service-to-service calls work
- Configuration hierarchy in place

✅ **DevOps:**
- Setup script for easy re-configuration
- .gitignore prevents future leaks
- Clean commit history

### Where Are Secrets Stored?

**Local development (User Secrets):**
```
C:\Users\minha\AppData\Roaming\Microsoft\UserSecrets\
├── <AuthAPI-ID>\secrets.json
├── <ProductAPI-ID>\secrets.json
├── <CouponAPI-ID>\secrets.json
├── <ShoppingCartAPI-ID>\secrets.json
└── <EmailAPI-ID>\secrets.json
```

**Production (Phase 4 - not yet implemented):**
- Azure Key Vault: `ecommerce-secrets-kv`

---

## 🔧 Troubleshooting

### Issue: Service won't start after changes

**Error:** `InvalidOperationException: ServiceBusConnectionString not configured`

**Solution:**
```bash
# Re-run user secrets for that service
cd E-commerce.Services.ShoppingCartAPI
dotnet user-secrets list  # Check what's currently set
dotnet user-secrets set "ServiceBusConnectionString" "Endpoint=sb://ecommerceweb..."
```

---

### Issue: MessageBus compilation error

**Error:** `IConfiguration does not exist in the current context`

**Solution:**
```bash
cd Ecommerce.MessageBus
dotnet add package Microsoft.Extensions.Configuration.Abstractions
dotnet build
```

---

### Issue: Secrets not being read

**Error:** Connection string is empty at runtime

**Check:**
1. Is `ASPNETCORE_ENVIRONMENT` set to `Development`?
   ```bash
   echo $env:ASPNETCORE_ENVIRONMENT  # PowerShell
   # Should output: Development (or blank, which defaults to Development)
   ```

2. Are secrets actually set?
   ```bash
   cd ServiceName
   dotnet user-secrets list
   ```

3. Does the key match exactly?
   - AuthAPI uses `ApiSettings:JwtOptions:Secret` (nested)
   - Others use `ApiSettings:Secret` (flat)

---

### Issue: Want to reset everything

**Solution:**
```bash
# Clear all user secrets for one service
cd E-commerce.Services.ProductAPI
dotnet user-secrets clear

# Re-run setup script
cd ../..
.\scripts\setup-user-secrets.ps1
```

---

## 📊 Verification Checklist

Before moving to Phase 2, confirm:

- [ ] Script ran successfully (5/5 services configured)
- [ ] MessageBus.cs builds without errors
- [ ] All appsettings.json files have empty strings for secrets
- [ ] ProductAPI starts and shows products in Swagger
- [ ] CouponAPI starts and shows coupons in Swagger
- [ ] AuthAPI starts without errors
- [ ] ShoppingCartAPI can call ProductAPI and CouponAPI
- [ ] EmailAPI starts and Service Bus consumers initialize
- [ ] `git diff` shows no secrets being added
- [ ] Changes committed to Git
- [ ] (Optional) Pushed to remote repository

**Status:** 🟢 Phase 1 Complete

---

## 📚 Next Steps

**Phase 2: Containerization** (See DEPLOYMENT-PLAN.md)
- Create Dockerfiles for all services
- Create docker-compose.yml
- Test local container deployment

**Estimated time for Phase 2:** 3-4 hours

---

## 🆘 Need Help?

**To re-run Phase 1:**
```bash
# Revert to backup
git checkout backup-before-phase1

# Or just re-run the script
.\scripts\setup-user-secrets.ps1
```

**Reference files:**
- User Secrets script: `scripts/setup-user-secrets.ps1`
- Deployment plan: `DEPLOYMENT-PLAN.md`
- Project architecture: `CLAUDE.md`

---

**Last Updated:** 2025-11-09
**Time Investment:** ~45 minutes
**Next Phase:** Containerization (DEPLOYMENT-PLAN.md Phase 2)
