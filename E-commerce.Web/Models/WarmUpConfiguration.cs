namespace E_commerce.Web.Models
{
    public class WarmUpConfiguration
    {
        public bool Enabled { get; set; } = true;
        public List<ServiceWarmUpConfig> Services { get; set; } = new();
    }

    public class ServiceWarmUpConfig
    {
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string HealthEndpoint { get; set; } = "/health";
        public int TimeoutMs { get; set; } = 30000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 2000;
    }
}
