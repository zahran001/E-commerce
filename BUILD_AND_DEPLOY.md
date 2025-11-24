# Docker Build and Push Instructions

This document provides step-by-step instructions for building and pushing Docker images to Azure Container Registry (ACR).

## Prerequisites

- Azure CLI installed
- Docker installed and running
- Logged into ACR: `az acr login --name ecommerceacr`

---

## Quick Start

If already logged into ACR, run all build commands from repo root, then all push commands.

---

## Part A: Login to ACR

```powershell
az acr login --name ecommerceacr
```

**Expected output:**
```
Login Succeeded
```

---

## Part B: Build All 6 Docker Images

Run these commands one at a time in PowerShell from the repository root: `c:\Users\minha\source\repos\E-commerce`

### 1. AuthAPI
```powershell
docker build -t ecommerceacr.azurecr.io/authapi:1.0.1 -t ecommerceacr.azurecr.io/authapi:latest -f E-commerce.Services.AuthAPI\Dockerfile .
```

### 2. ProductAPI
```powershell
docker build -t ecommerceacr.azurecr.io/productapi:1.0.1 -t ecommerceacr.azurecr.io/productapi:latest -f E-commerce.Services.ProductAPI\Dockerfile .
```

### 3. CouponAPI
```powershell
docker build -t ecommerceacr.azurecr.io/couponapi:1.0.1 -t ecommerceacr.azurecr.io/couponapi:latest -f E-commerce.Services.CouponAPI\Dockerfile .
```

### 4. ShoppingCartAPI
```powershell
docker build -t ecommerceacr.azurecr.io/shoppingcartapi:1.0.1 -t ecommerceacr.azurecr.io/shoppingcartapi:latest -f E-commerce.Services.ShoppingCartAPI\Dockerfile .
```

### 5. EmailAPI
```powershell
docker build -t ecommerceacr.azurecr.io/emailapi:1.0.1 -t ecommerceacr.azurecr.io/emailapi:latest -f Ecommerce.Services.EmailAPI\Dockerfile .
```

### 6. Web MVC
```powershell
docker build -t ecommerceacr.azurecr.io/web:1.0.1 -t ecommerceacr.azurecr.io/web:latest -f E-commerce.Web\Dockerfile .
```

**Command breakdown:**
- `-t ecommerceacr.azurecr.io/[service]:1.0.1` - Tags image with version 1.0.1
- `-t ecommerceacr.azurecr.io/[service]:latest` - Tags same image as latest
- `-f [Dockerfile path]` - Specifies which Dockerfile to use
- `.` - Builds from current directory (repo root)

**Expected output for each build:**
```
=> => naming to ecommerceacr.azurecr.io/[service]:1.0.1
=> => naming to ecommerceacr.azurecr.io/[service]:latest
```

**Build time:** 30-60 seconds per image

---

## Part C: Push All Images to ACR

Run these commands in PowerShell after all builds complete:

```powershell
docker push ecommerceacr.azurecr.io/authapi:1.0.1
docker push ecommerceacr.azurecr.io/authapi:latest

docker push ecommerceacr.azurecr.io/productapi:1.0.1
docker push ecommerceacr.azurecr.io/productapi:latest

docker push ecommerceacr.azurecr.io/couponapi:1.0.1
docker push ecommerceacr.azurecr.io/couponapi:latest

docker push ecommerceacr.azurecr.io/shoppingcartapi:1.0.1
docker push ecommerceacr.azurecr.io/shoppingcartapi:latest

docker push ecommerceacr.azurecr.io/emailapi:1.0.1
docker push ecommerceacr.azurecr.io/emailapi:latest

docker push ecommerceacr.azurecr.io/web:1.0.1
docker push ecommerceacr.azurecr.io/web:latest
```

**Expected output for each push:**
```
1.0.1: digest: sha256:...
latest: digest: sha256:...
```

**Push time:** 2-5 minutes total (parallel uploads)

---

## Part D: Verify Images in ACR (Optional)

List all repositories:
```powershell
az acr repository list --name ecommerceacr
```

**Expected output:**
```
[
  "authapi",
  "couponapi",
  "emailapi",
  "productapi",
  "shoppingcartapi",
  "web"
]
```

Check tags for a specific image:
```powershell
az acr repository show-tags --name ecommerceacr --repository authapi
```

**Expected output:**
```
[
  "1.0.0",
  "1.0.1",
  "latest"
]
```

---

## Version Numbering

When building new images, update the version number in all commands:

| Version | When to Use |
|---------|------------|
| `1.0.0` | Initial release (already pushed) |
| `1.0.1` | Bug fixes, configuration changes |
| `1.1.0` | New features, minor updates |
| `2.0.0` | Major breaking changes |

**Always tag both the version number AND `latest`** for easy rollback.

---

## Troubleshooting

### Docker build fails
- Ensure you're in the repo root directory
- Check Dockerfile paths are correct (backslashes for Windows)
- Run `docker system prune` to free up space if disk is full

### Push fails with 401 Unauthorized
- Run `az acr login --name ecommerceacr` again
- Check Azure credentials are valid

### Push fails with network timeout
- Check internet connection
- Try pushing individual images instead of batch
- Images are large (~500MB each); allow 5-10 minutes per push

### Want to see image details
```powershell
docker images | grep ecommerceacr
```

---

## Next Steps

After pushing all images:

1. Update PHASE4-PROGRESS.md with timestamp and version
2. Proceed to Phase 4.5 (Create Container Apps Environment)
3. Deploy services in Phase 4.6

---

**Last Updated:** 2025-11-20
**Applicable Versions:** v1.0.1+
