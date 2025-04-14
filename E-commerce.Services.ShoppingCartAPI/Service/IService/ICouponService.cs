using E_commerce.Services.ShoppingCartAPI.Models.Dto;

namespace E_commerce.Services.ShoppingCartAPI.Service.IService
{
    public interface ICouponService
    {
        Task<CouponDto> GetCoupon(string couponCode);
    }
}
