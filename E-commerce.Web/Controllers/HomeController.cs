using E_commerce.Web.Models;
using E_commerce.Web.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;

namespace E_commerce.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductService _productService;
        private readonly ICartService _cartService;

        public HomeController(IProductService productService, ICartService cartService)
        {
            _productService = productService;
            _cartService = cartService;
        }

        public async Task<IActionResult> Index()
        {
			List<ProductDto>? list = new();

			ResponseDto? response = await _productService.GetAllProductsAsync();

			if (response != null && response.IsSuccess)
			{
				// ResponseDto.Result is an object, so we need to deserialize the object.
				// T obj = JsonConvert.DeserializeObject<T>(jsonString);

				list = JsonConvert.DeserializeObject<List<ProductDto>>(Convert.ToString(response.Result));
			}
			else
			{
				TempData["error"] = response?.Message; // null check
			}
			return View(list);
		}

        [Authorize]
        public async Task<IActionResult> ProductDetails(int productId)
        {
            ProductDto? model = new();

            ResponseDto? response = await _productService.GetProductByIdAsync(productId);

            if (response != null && response.IsSuccess)
            {

                model = JsonConvert.DeserializeObject<ProductDto>(Convert.ToString(response.Result));
            }
            else
            {
                TempData["error"] = response?.Message; // null check
            }
            return View("ProductDetails", model);
        }

        //  This endpoint takes a product selection from the user, builds up a cart DTO
        //  (linking it to the logged in user via their JWT),
        //  calls the cart service to add or update that item in their cart,
        //  and then either redirects with a success notice or redisplays the form with an error.
        [Authorize]
        [HttpPost]
        [ActionName("ProductDetails")]
        public async Task<IActionResult> ProductDetails(ProductDto productDto)
        {
            // Populate cartDto - cart header and cart details - based on the productId
            CartDto cartDto = new CartDto()
            {
                CartHeader = new CartHeaderDto()
                {
                    UserId = User.Claims.Where(u=>u.Type == JwtRegisteredClaimNames.Sub).FirstOrDefault()?.Value,
                }
            };

            CartDetailsDto cartDetails = new CartDetailsDto()
            {
                ProductId = productDto.ProductId,
                Count = productDto.Count,
            };
            
            List<CartDetailsDto> cartDetailsDtos = new() { cartDetails };
            cartDto.CartDetails = cartDetailsDtos;
            // Wraps the posted product ID and quantity into a CartDetailsDto list on the CartDto.

            ResponseDto? response = await _cartService.UpsertCartAsync(cartDto); // insert a new cart or update an existing one

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Item added to cart";

                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["error"] = response?.Message; // null check
            }
            return View(productDto);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
