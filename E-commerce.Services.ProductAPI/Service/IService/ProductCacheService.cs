using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;
using E_commerce.Services.ProductAPI.Configuration;

namespace E_commerce.Services.ProductAPI.Service.IService
{
    public class ProductCacheService : IProductCacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<ProductCacheService> _logger;
        private readonly int _defaultDurationSeconds;
        private readonly bool _slidingExpiration;

        public ProductCacheService(
            IDistributedCache cache,
            ILogger<ProductCacheService> logger,
            IOptions<CacheSettings> cacheSettings)
        {
            _cache = cache;
            _logger = logger;
            _defaultDurationSeconds = cacheSettings.Value.DefaultCacheDuration;
            _slidingExpiration = cacheSettings.Value.SlidingExpiration;
        }

        public async Task<T> GetAsync<T>(string key)
        {
            try
            {
                var cachedValue = await _cache.GetStringAsync(key);

                if (cachedValue == null)
                {
                    _logger.LogInformation("Cache MISS for key: {Key}", key);
                    return default;
                }

                _logger.LogInformation("Cache HIT for key: {Key}", key);
                return JsonSerializer.Deserialize<T>(cachedValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cache for key: {Key}", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    // Absolute Expiration: The hard limit (e.g., 1 hour from now)
                    AbsoluteExpirationRelativeToNow =
                        expiration ?? TimeSpan.FromSeconds(_defaultDurationSeconds),

                    // Sliding Expiration: If accessed within this window (e.g., 30 mins), 
                    // the timer resets. Keeps frequently accessed data alive longer.
                    SlidingExpiration = _slidingExpiration
                        ? TimeSpan.FromSeconds(_defaultDurationSeconds / 2)
                        : null
                };

                // Serialize the C# object into a JSON string because Redis stores text/bytes
                var serializedValue = JsonSerializer.Serialize(value);

                // Send the string to Redis with the configured options
                await _cache.SetStringAsync(key, serializedValue, cacheOptions);

                _logger.LogInformation(
                    "Cache SET for key: {Key}, Duration: {Duration}s",
                    key,
                    _defaultDurationSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _cache.RemoveAsync(key);
                _logger.LogInformation("Cache REMOVED for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                // Note: IDistributedCache doesn't support pattern-based deletion natively
                // This would require custom implementation using StackExchange.Redis directly
                _logger.LogInformation("Cache PATTERN REMOVAL requested for pattern: {Pattern}", pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by pattern: {Pattern}", pattern);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var value = await _cache.GetStringAsync(key);
                return value != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return false;
            }
        }
    }
}
