# Phase 5: Azure Portal Manual Deployment Guide

Complete step-by-step instructions to deploy the observability infrastructure (Redis, Seq, Jaeger) using the Azure Portal instead of PowerShell scripts.

---

## Phase 5.1: Create Storage Infrastructure

### Step 1: Create Storage Account

**Navigate to Azure Portal:**
1. Go to https://portal.azure.com
2. Search for **"Storage accounts"** in the search bar
3. Click **"+ Create"** button

**Configure Storage Account:**
- **Subscription:** Select your subscription
- **Resource Group:** `Ecommerce-Project`
- **Storage account name:** `ecommerceobservability`
- **Region:** `East US` (same as your Container Apps environment)
- **Performance:** `Standard`
- **Redundancy:** `Locally-redundant storage (LRS)`

**Advanced Tab:**
- **Require secure transfer for REST API operations:** Enable (checked)
- **Allow storage account key access:** Enable (checked)
- **Minimum TLS version:** `Version 1.2`
- **Blob public access:** `Disabled`

**Click "Review + Create"** → **"Create"**

**Expected time:** 2-3 minutes

---

### Step 2: Create File Share for Seq Data

**Navigate to Storage Account:**
1. Go to the newly created storage account: `ecommerceobservability`
2. Left sidebar → **"File shares"** (under Data storage)
3. Click **"+ File share"** button

**Configure File Share:**
- **Name:** `seq-data`
- **Tier:** `Transaction optimized`
- **Quota:** `32` GiB

**Click "Create"**

**Expected time:** 1 minute

---

### Step 3: Add Directory to File Share (Optional but recommended)

**Navigate to File Share:**
1. Click on the newly created file share: `seq-data`
2. Click **"New folder"** button

**Configure Folder:**
- **Name:** `data`

**Click "Create"**

**Expected time:** 30 seconds

---

### Step 4: Get Storage Account Key (Save for later)

**Navigate to Storage Account:**
1. Go to `ecommerceobservability` storage account
2. Left sidebar → **"Access keys"** (under Security + networking)
3. Copy the **Key1** value (primary key)
4. **Save this key** - you'll need it in Step 7

**Expected time:** 1 minute

---

## Phase 5.2: Configure Storage Mount in Container Apps Environment

### Step 5: Add Storage Mount to Container Apps Environment

**Navigate to Container Apps Environment:**
1. Search for **"Container Apps environments"** in search bar
2. Click on `ecommerce-env`
3. Left sidebar → **"Storage"** (under Settings)
4. Click **"+ Add"** button

**Configure Storage Mount:**
- **Name:** `seq-storage`
- **Storage type:** `Azure Files`
- **Account name:** `ecommerceobservability`
- **Account key:** [Paste the key from Step 4]
- **Share name:** `seq-data`
- **Access mode:** `ReadWrite`

**Click "Add"**

**Expected time:** 2 minutes

---

## Phase 5.3: Deploy Redis Container App

### Step 6: Create Redis Container App

**Navigate to Container Apps:**
1. Search for **"Container Apps"** in search bar
2. Click **"+ Create"** button

**Basics Tab:**
- **Subscription:** Your subscription
- **Resource Group:** `Ecommerce-Project`
- **Container app name:** `redis`
- **Region:** `East US`
- **Container Apps Environment:** `ecommerce-env`

**Container Tab:**
- Click **"+ Add"** button

**Configure Container:**
- **Name:** `redis`
- **Image source:** `Docker Hub or other registries`
- **Image:** `redis:7-alpine`
- **CPU and memory:** `0.25 CPU cores` / `0.5 Gi memory`

**Environment Variables:**
- Click **"+ Add"** for each variable:
  - Name: `REDIS_MAXMEMORY` → Value: `256mb`
  - Name: `REDIS_MAXMEMORY_POLICY` → Value: `allkeys-lru`

**Click "Add" (to add container)**

**Ingress Tab:**
- **Ingress:** Enable (checked)
- **Ingress type:** `Internal`
- **Target port:** `6379`

**Scale Tab:**
- **Min replicas:** `0` (scale-to-zero)
- **Max replicas:** `1`

**Review + Create** → **Create**

**Expected time:** 3-5 minutes

---

## Phase 5.4: Deploy Seq Container App

### Step 7: Create Seq Container App

**Navigate to Container Apps:**
1. Search for **"Container Apps"** in search bar
2. Click **"+ Create"** button

**Basics Tab:**
- **Subscription:** Your subscription
- **Resource Group:** `Ecommerce-Project`
- **Container app name:** `seq`
- **Region:** `East US`
- **Container Apps Environment:** `ecommerce-env`

**Container Tab:**
- Click **"+ Add"** button

**Configure Container:**
- **Name:** `seq`
- **Image source:** `Docker Hub or other registries`
- **Image:** `datalust/seq:latest`
- **CPU and memory:** `0.25 CPU cores` / `0.5 Gi memory`

**Environment Variables:**
- Click **"+ Add"** for each variable:
  - Name: `ACCEPT_EULA` → Value: `Y`
  - Name: `SEQ_CACHE_SYSTEMRAMTARGET` → Value: `0.4`

**Volume Mounts:**
- Click **"+ Add"**
  - **Storage name:** `seq-storage`
  - **Mount path:** `/data`

**Click "Add" (to add container)**

**Ingress Tab:**
- **Ingress:** Enable (checked)
- **Ingress type:** `Internal`
- **Target port:** `80`

**Scale Tab:**
- **Min replicas:** `1` (always running)
- **Max replicas:** `1`

**Review + Create** → **Create**

**Expected time:** 3-5 minutes

---

## Phase 5.5: Deploy Jaeger Container App

### Step 8: Create Jaeger Container App

**Navigate to Container Apps:**
1. Search for **"Container Apps"** in search bar
2. Click **"+ Create"** button

**Basics Tab:**
- **Subscription:** Your subscription
- **Resource Group:** `Ecommerce-Project`
- **Container app name:** `jaeger`
- **Region:** `East US`
- **Container Apps Environment:** `ecommerce-env`

**Container Tab:**
- Click **"+ Add"** button

**Configure Container:**
- **Name:** `jaeger`
- **Image source:** `Docker Hub or other registries`
- **Image:** `jaegertracing/all-in-one:latest`
- **CPU and memory:** `0.25 CPU cores` / `0.5 Gi memory`

**Environment Variables:**
- Click **"+ Add"** for each variable:
  - Name: `COLLECTOR_OTLP_ENABLED` → Value: `true`
  - Name: `SPAN_STORAGE_TYPE` → Value: `memory`

**Click "Add" (to add container)**

**Ingress Tab:**
- **Ingress:** Enable (checked)
- **Ingress type:** `Internal`
- **Target port:** `16686`

**Scale Tab:**
- **Min replicas:** `0` (scale-to-zero)
- **Max replicas:** `1`

**Review + Create** → **Create**

**Expected time:** 3-5 minutes

---

## Phase 5.6: Update Microservices with Observability Configuration

### Step 9: Update ProductAPI Environment Variables

**Navigate to Container App:**
1. Search for **"Container Apps"** in search bar
2. Click on `productapi`

**Update Configuration:**
1. Left sidebar → **"Containers"** (under Settings)
2. Click on the container name: `productapi-*`
3. Scroll down to **"Environment variables"** section
4. Click **"+ Add"** for each new variable:

**Add these environment variables:**
```
CacheSettings__RedisConnection = redis:6379
CacheSettings__Enabled = true
Serilog__WriteTo__2__Args__serverUrl = http://seq:80
Jaeger__AgentHost = jaeger
Jaeger__AgentPort = 6831
```

5. Click **"Save"** at the top right

**Expected time:** 2 minutes

---

### Step 10: Update AuthAPI Environment Variables

**Navigate to Container App:**
1. Click on `authapi`

**Update Configuration:**
1. Left sidebar → **"Containers"** (under Settings)
2. Click on the container name: `authapi-*`
3. Scroll down to **"Environment variables"** section
4. Click **"+ Add"** for each new variable:

**Add these environment variables:**
```
Serilog__WriteTo__2__Args__serverUrl = http://seq:80
Jaeger__AgentHost = jaeger
Jaeger__AgentPort = 6831
```

5. Click **"Save"** at the top right

**Expected time:** 2 minutes

---

### Step 11: Update CouponAPI Environment Variables

**Navigate to Container App:**
1. Click on `couponapi`

**Update Configuration:**
1. Left sidebar → **"Containers"** (under Settings)
2. Click on the container name: `couponapi-*`
3. Add the same variables as AuthAPI:

```
Serilog__WriteTo__2__Args__serverUrl = http://seq:80
Jaeger__AgentHost = jaeger
Jaeger__AgentPort = 6831
```

5. Click **"Save"** at the top right

**Expected time:** 2 minutes

---

### Step 12: Update ShoppingCartAPI Environment Variables

**Navigate to Container App:**
1. Click on `shoppingcartapi`

**Update Configuration:**
1. Left sidebar → **"Containers"** (under Settings)
2. Click on the container name: `shoppingcartapi-*`
3. Add the same variables as AuthAPI:

```
Serilog__WriteTo__2__Args__serverUrl = http://seq:80
Jaeger__AgentHost = jaeger
Jaeger__AgentPort = 6831
```

5. Click **"Save"** at the top right

**Expected time:** 2 minutes

---

### Step 13: Update EmailAPI Environment Variables

**Navigate to Container App:**
1. Click on `emailapi`

**Update Configuration:**
1. Left sidebar → **"Containers"** (under Settings)
2. Click on the container name: `emailapi-*`
3. Add the same variables as AuthAPI:

```
Serilog__WriteTo__2__Args__serverUrl = http://seq:80
Jaeger__AgentHost = jaeger
Jaeger__AgentPort = 6831
```

5. Click **"Save"** at the top right

**Expected time:** 2 minutes

---

### Step 14: Update Web MVC Environment Variables

**Navigate to Container App:**
1. Click on `web`

**Update Configuration:**
1. Left sidebar → **"Containers"** (under Settings)
2. Click on the container name: `web-*`
3. Add the same variables as AuthAPI:

```
Serilog__WriteTo__2__Args__serverUrl = http://seq:80
Jaeger__AgentHost = jaeger
Jaeger__AgentPort = 6831
```

5. Click **"Save"** at the top right

**Expected time:** 2 minutes

---

## Phase 5.7: Verify Deployment

### Step 15: Verify All Container Apps are Running

**Navigate to Container Apps:**
1. Search for **"Container Apps"** in search bar
2. You should see 9 apps total:
   - redis ✓
   - seq ✓
   - jaeger ✓
   - authapi ✓
   - productapi ✓
   - couponapi ✓
   - shoppingcartapi ✓
   - emailapi ✓
   - web ✓

**Check Status:**
- Click each app to verify the **Status** shows **"Running"**
- Check the **Revisions** tab to confirm latest revision is active

**Expected time:** 2 minutes

---

### Step 16: Access Seq (Optional)

**Navigate to Seq:**
1. Click on the `seq` container app
2. Left sidebar → **"Overview"**
3. Note the **Application Url** (e.g., `https://seq.internal.mangosea-a7508352.eastus.azurecontainerapps.io`)
4. You can access this URL if you have internal network access (VPN or Bastion)

**Expected time:** 1 minute

---

### Step 17: Access Jaeger (Optional)

**Navigate to Jaeger:**
1. Click on the `jaeger` container app
2. Left sidebar → **"Overview"**
3. Note the **Application Url** (e.g., `https://jaeger.internal.mangosea-a7508352.eastus.azurecontainerapps.io`)
4. You can access this URL if you have internal network access (VPN or Bastion)

**Expected time:** 1 minute

---

## Troubleshooting

### Issue: Container App shows "Provisioning" status

**Solution:** Wait 2-3 minutes, then refresh the page. Container Apps can take time to provision.

### Issue: "Failed to add storage mount"

**Diagnosis:**
1. Verify the storage account name is correct: `ecommerceobservability`
2. Verify the storage account key is correct (copy from "Access keys" section)
3. Verify the file share name is correct: `seq-data`

**Solution:** Delete the mount and try again with correct values.

### Issue: Container App won't start after environment variable update

**Solution:**
1. Check the container logs: Container app → **"Console"** tab
2. Look for error messages related to connection strings
3. Verify all environment variable names are spelled correctly (case-sensitive)
4. Verify all environment variable values are correct (no trailing spaces)

### Issue: Can't see logs in Seq

**Solution:**
1. Verify ProductAPI has the correct Serilog URL: `http://seq:80`
2. Trigger some API calls to generate logs
3. Wait 30 seconds for logs to appear
4. Access Seq UI and search for recent events

### Issue: Can't see traces in Jaeger

**Solution:**
1. Verify services have correct Jaeger configuration:
   - `Jaeger__AgentHost = jaeger`
   - `Jaeger__AgentPort = 6831`
2. Trigger some API calls to generate traces
3. Wait 30 seconds for traces to appear
4. Access Jaeger UI and select a service from the dropdown

---

## Summary of Manual Steps

| Step | Component | Action | Time |
|------|-----------|--------|------|
| 1 | Storage Account | Create `ecommerceobservability` | 2-3 min |
| 2 | File Share | Create `seq-data` | 1 min |
| 3 | File Share | Create `data` folder | 30 sec |
| 4 | Storage Account | Copy access key | 1 min |
| 5 | Container Apps Env | Add storage mount `seq-storage` | 2 min |
| 6 | Container App | Deploy `redis` | 3-5 min |
| 7 | Container App | Deploy `seq` | 3-5 min |
| 8 | Container App | Deploy `jaeger` | 3-5 min |
| 9-14 | Container Apps | Update 6 service env vars | 12 min |
| 15 | Verification | Check all apps running | 2 min |
| 16 | Verification | Access Seq (optional) | 1 min |
| 17 | Verification | Access Jaeger (optional) | 1 min |

**Total Time: 40-50 minutes**

---

## Cost Verification

After deployment, verify your monthly cost:

**Navigate to Cost Analysis:**
1. Search for **"Cost Management + Billing"**
2. Left sidebar → **"Cost Analysis"**
3. Filter by Resource Group: `Ecommerce-Project`
4. Expected cost: ~$21/month total
   - Redis: ~$2/month
   - Seq: ~$7/month
   - Jaeger: ~$1/month
   - Storage: ~$1.50/month
   - Phase 4 services: ~$9/month

---

## Post-Deployment Checklist

- [ ] Storage account `ecommerceobservability` created
- [ ] File share `seq-data` created with 32GB quota
- [ ] Storage mount `seq-storage` added to Container Apps environment
- [ ] Redis container app deployed and running
- [ ] Seq container app deployed and running
- [ ] Jaeger container app deployed and running
- [ ] All 6 microservices updated with environment variables
- [ ] Redis cache tested (ProductAPI responses faster)
- [ ] Seq receiving logs from all services
- [ ] Jaeger showing traces from all services
- [ ] Cost monitoring shows expected charges

---

**Last Updated:** 2025-12-30
**Estimated Total Time:** 40-50 minutes
**Rollback Time (if needed):** 10 minutes
