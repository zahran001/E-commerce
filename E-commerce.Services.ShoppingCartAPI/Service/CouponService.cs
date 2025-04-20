using E_commerce.Services.ShoppingCartAPI.Models.Dto;
using E_commerce.Services.ShoppingCartAPI.Service.IService;
using Newtonsoft.Json;

namespace E_commerce.Services.ShoppingCartAPI.Service
{
    public class CouponService : ICouponService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public CouponService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<CouponDto> GetCoupon(string couponCode)
        {
            var client = _httpClientFactory.CreateClient("Coupon");
            // Based on the name, it will examine Program.cs and get the base address.
            var response = await client.GetAsync($"/api/coupon/GetByCode/{couponCode}");
            // Reading and deserializing the response
            var apiContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ResponseDto>(apiContent);
            if (apiResponse!=null && apiResponse.IsSuccess)
            {
                return JsonConvert.DeserializeObject<CouponDto>(Convert.ToString(apiResponse.Result));
            }
            return new CouponDto();

        }
    }
}
