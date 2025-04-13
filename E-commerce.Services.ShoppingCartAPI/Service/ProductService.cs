using E_commerce.Services.ShoppingCartAPI.Models.Dto;
using E_commerce.Services.ShoppingCartAPI.Service.IService;
using Newtonsoft.Json;

namespace E_commerce.Services.ShoppingCartAPI.Service
{
    public class ProductService : IProductService
    {
        // Injecting IHttpClientFactory to make http call
        private readonly IHttpClientFactory _httpClientFactory;

        public ProductService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IEnumerable<ProductDto>> GetProducts()
        {
            var client = _httpClientFactory.CreateClient("Product");
            // Based on the name, it will examine Program.cs and get the base address.
            var response = await client.GetAsync($"/api/product");
            var apiContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ResponseDto>(apiContent);
            if (apiResponse.IsSuccess)
            {
                return JsonConvert.DeserializeObject<IEnumerable<ProductDto>>(Convert.ToString(apiResponse.Result));
            }
            return new List<ProductDto>();
            
        }
    }
}
