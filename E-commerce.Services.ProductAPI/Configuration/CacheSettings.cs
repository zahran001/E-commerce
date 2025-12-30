namespace E_commerce.Services.ProductAPI.Configuration
{
    public class CacheSettings
    {
        public bool Enabled { get; set; } = true;
        public string RedisConnection { get; set; } = "localhost:6379";
        public int DefaultCacheDuration { get; set; } = 3600; // 1 hour
        public bool SlidingExpiration { get; set; } = true;
    }
}
