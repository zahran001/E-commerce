using E_commerce.Web.Models;

namespace E_commerce.Web.Service.IService
{
    public interface ICartService
    {
        Task<ResponseDto?> GetCartByUserIdAsync(string userId);
        Task<ResponseDto?> UpsertCartAsync(CartDto cartDto);
        Task<ResponseDto?> RemoveFromCartAsync(int cartDetailsId);
        Task<ResponseDto?> ApplyCouponAsync(CartDto cartDto);
    }
}

// When we are working with the coupon service, we need CouponDto.
// We are getting all the coupons - type of that model will be CouponDto.
