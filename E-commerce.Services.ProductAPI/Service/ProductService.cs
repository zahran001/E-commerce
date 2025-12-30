using AutoMapper;
using E_commerce.Services.ProductAPI.Data;
using E_commerce.Services.ProductAPI.Models;
using E_commerce.Services.ProductAPI.Models.Dto;
using E_commerce.Services.ProductAPI.Service.IService;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Services.ProductAPI.Service
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMapper _mapper;
        private readonly IProductCacheService _cacheService;
        private readonly ILogger<ProductService> _logger;

        // Cache key constants - hierarchical naming convention
        private const string ALL_PRODUCTS_CACHE_KEY = "product:all";
        private const string PRODUCT_BY_ID_CACHE_KEY = "product:id:{0}";

        public ProductService(
            ApplicationDbContext db,
            IMapper mapper,
            IProductCacheService cacheService,
            ILogger<ProductService> logger)
        {
            _db = db;
            _mapper = mapper;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<List<ProductDto>> GetAllProductsAsync()
        {
            try
            {
                // Attempt to get from cache
                var cachedProducts = await _cacheService
                    .GetAsync<List<ProductDto>>(ALL_PRODUCTS_CACHE_KEY);

                if (cachedProducts != null)
                {
                    _logger.LogInformation(
                        "Retrieved {Count} products from cache",
                        cachedProducts.Count);
                    return cachedProducts;
                }

                // Cache miss - query database
                _logger.LogInformation("Cache miss for all products, querying database");

                // query the SQL database
                var products = await _db.Products.ToListAsync();
                
                // Convert raw DB entities into cleaner DTOs for the client
                var productDtos = _mapper.Map<List<ProductDto>>(products);

                // Cache the fresh data into Redis for the next request
                await _cacheService.SetAsync(ALL_PRODUCTS_CACHE_KEY, productDtos);

                _logger.LogInformation(
                    "Cached {Count} products, cache key: {Key}",
                    productDtos.Count,
                    ALL_PRODUCTS_CACHE_KEY);

                return productDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all products");
                throw;
            }
        }

        public async Task<ProductDto> GetProductByIdAsync(int id)
        {
            try
            {
                var cacheKey = string.Format(PRODUCT_BY_ID_CACHE_KEY, id);

                // Attempt to get from cache
                var cachedProduct = await _cacheService
                    .GetAsync<ProductDto>(cacheKey);

                if (cachedProduct != null)
                {
                    _logger.LogInformation(
                        "Retrieved product {ProductId} from cache",
                        id);
                    return cachedProduct;
                }

                // Cache miss - query database
                _logger.LogInformation(
                    "Cache miss for product {ProductId}, querying database",
                    id);
                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found", id);
                    return null;
                }

                var productDto = _mapper.Map<ProductDto>(product);

                // Cache the result
                await _cacheService.SetAsync(cacheKey, productDto);

                _logger.LogInformation(
                    "Cached product {ProductId}, cache key: {Key}",
                    id,
                    cacheKey);

                return productDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product {ProductId}", id);
                throw;
            }
        }

        public async Task<ProductDto> CreateProductAsync(ProductDto productDto)
        {
            try
            {
                var product = _mapper.Map<Product>(productDto);
                _db.Products.Add(product);
                await _db.SaveChangesAsync();

                // Map back to DTO with generated ID
                var createdDto = _mapper.Map<ProductDto>(product);

                // Invalidate related caches
                await InvalidateProductCaches(product);

                _logger.LogInformation(
                    "Created product {ProductId} and invalidated cache",
                    product.ProductId);

                return createdDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                throw;
            }
        }

        public async Task<bool> UpdateProductAsync(ProductDto productDto)
        {
            try
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.ProductId == productDto.ProductId);

                if (product == null)
                {
                    _logger.LogWarning(
                        "Product {ProductId} not found for update",
                        productDto.ProductId);
                    return false;
                }

                // Update properties
                _mapper.Map(productDto, product);
                await _db.SaveChangesAsync();

                // Invalidate related caches
                await InvalidateProductCaches(product);

                _logger.LogInformation(
                    "Updated product {ProductId} and invalidated cache",
                    product.ProductId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", productDto.ProductId);
                throw;
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            try
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found for deletion", id);
                    return false;
                }

                _db.Products.Remove(product);
                await _db.SaveChangesAsync();

                // Invalidate related caches
                await InvalidateProductCaches(product);

                _logger.LogInformation(
                    "Deleted product {ProductId} and invalidated cache",
                    id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", id);
                throw;
            }
        }

        /// <summary>
        /// Invalidates cache entries related to a product
        /// </summary>
        private async Task InvalidateProductCaches(Product product)
        {
            var tasks = new List<Task>
            {
                _cacheService.RemoveAsync(string.Format(PRODUCT_BY_ID_CACHE_KEY, product.ProductId)),
                _cacheService.RemoveAsync(ALL_PRODUCTS_CACHE_KEY)
            };

            await Task.WhenAll(tasks);
        }
    }
}
