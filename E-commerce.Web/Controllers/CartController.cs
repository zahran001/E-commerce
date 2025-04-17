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

        private async Task<CartDto>  LoadCartDtoBasedOnLoggedInUser()
        {
            var userId = User.Claims.Where(u=>u.Type == JwtRegisteredClaimNames.Sub)?.FirstOrDefault()?.Value;
            ResponseDto? response = await _cartService.GetCartByUserIdAsync(userId);
            if (response != null && response.IsSuccess)
            {
                CartDto cartDto = JsonConvert.DeserializeObject<CartDto>(Convert.ToString(response.Result));
                return cartDto;
            }
            return new CartDto();
        }
    }
}
