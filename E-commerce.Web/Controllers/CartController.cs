using E_commerce.Web.Models;
using E_commerce.Web.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;

namespace E_commerce.Web.Controllers
{
    public class CartController : Controller
    {
        // inject the cart service
        private readonly ICartService _cartService;
        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        [Authorize]
        public async Task<IActionResult> CartIndex()
        {
            // call the cart service based on the logged in user
            return View(await LoadCartDtoBasedOnLoggedInUser());
        }

        public async Task<IActionResult> Remove(int cartDetailsId)
        {
            // get the userId
            var userId = User.Claims.Where(u => u.Type == JwtRegisteredClaimNames.Sub)?.FirstOrDefault()?.Value;
            ResponseDto? response = await _cartService.RemoveFromCartAsync(cartDetailsId);
            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Product removed from cart successfully";
                return RedirectToAction(nameof(CartIndex));
            }
            return View();

        }

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(CartDto cartDto)
        {
            ResponseDto? response = await _cartService.ApplyCouponAsync(cartDto);

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Coupon applied";
                return RedirectToAction(nameof(CartIndex));
            }
            return View();

        }

        [HttpPost]
        public async Task<IActionResult> EmailCart(CartDto cartDto)
        {
            ResponseDto? response = await _cartService.EmailCart(cartDto);

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "You'll receive the email shortly.";
                return RedirectToAction(nameof(CartIndex));
            }
            return View();

        }


        [HttpPost]
        public async Task<IActionResult> RemoveCoupon(CartDto cartDto)
        {
            cartDto.CartHeader.CouponCode = "";
            ResponseDto? response = await _cartService.ApplyCouponAsync(cartDto);

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Coupon removed";
                return RedirectToAction(nameof(CartIndex));
            }
            return View();

        }



        private async Task<CartDto>  LoadCartDtoBasedOnLoggedInUser()
        {
            var userId = User.Claims.Where(u=>u.Type == JwtRegisteredClaimNames.Sub)?.FirstOrDefault()?.Value;
            ResponseDto? response = await _cartService.GetCartByUserIdAsync(userId);
            if (response.IsSuccess)
            {
                CartDto cartDto = JsonConvert.DeserializeObject<CartDto>(Convert.ToString(response.Result));
                return cartDto;
            }
            return new CartDto();
        }
    }
}
