namespace E_commerce.Services.ProductAPI.Service.IService
{
    /// <summary>
    /// Abstraction layer for distributed cache operations specific to products
    /// </summary>
    public interface IProductCacheService
    {
        /// <summary>
        /// Retrieves a cached value by key
        /// </summary>
        /// <typeparam name="T">Type of cached value</typeparam>
        /// <param name="key">Cache key</param>
        /// <returns>Cached value or null if not found</returns>
        Task<T> GetAsync<T>(string key);

        /// <summary>
        /// Sets a value in cache with optional expiration
        /// </summary>
        /// <typeparam name="T">Type of value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Optional expiration timespan</param>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>
        /// Removes a single key from cache
        /// </summary>
        /// <param name="key">Cache key to remove</param>
        Task RemoveAsync(string key);

        /// <summary>
        /// Removes multiple keys matching a pattern
        /// </summary>
        /// <param name="pattern">Pattern to match (e.g., "product:*")</param>
        Task RemoveByPatternAsync(string pattern);

        /// <summary>
        /// Checks if a key exists in cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True if key exists, false otherwise</returns>
        Task<bool> ExistsAsync(string key);
    }
}
