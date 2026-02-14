# Azure Cost Analysis

## Monthly Costs (Production)

### Microservices & Infrastructure (~$9/month)

| Resource | SKU/Tier | Monthly Cost |
|----------|----------|--------------|
| Azure Container Apps (6 microservices) | Consumption | ~$5 |
| Azure SQL Database (5 databases) | Serverless, auto-pause | ~$1 |
| Azure Service Bus (2 queues) | Basic | ~$1 |
| Azure Container Registry | Basic | ~$2 |
| SSL Certificate | Managed (included) | $0 |
| **Subtotal** | | **~$9/month** |

### Observability Stack (~$12/month)

| Resource | SKU/Tier | Monthly Cost |
|----------|----------|--------------|
| Redis Cache | Container Apps (0.25 vCPU, scale-to-zero) | ~$2 |
| Seq Logging | Container Apps (0.25 vCPU, 1 replica min, Azure Files) | ~$7 |
| Jaeger Tracing | Container Apps (0.25 vCPU, scale-to-zero) | ~$1 |
| Azure Files (32GB quota, Seq storage) | Standard tier | ~$1.50 |
| Storage Account | General Purpose v2 | ~$0.50 |
| **Subtotal** | | **~$12/month** |

### Total

| Category | Cost |
|----------|------|
| Microservices & Infrastructure | ~$9 |
| Observability Stack | ~$12 |
| **TOTAL** | **~$21/month** |

---

## Cost Optimization Strategies

- **SQL Database Serverless** - Auto-pause after idle period reduces costs significantly
- **Container Apps Consumption Plan** - Pay only for actual usage
- **Basic Tier Services** - Service Bus Basic tier sufficient for current load
- **Managed SSL Certificates** - Free with Container Apps
- **Scale-to-Zero** - Redis and Jaeger containers scale down when idle
