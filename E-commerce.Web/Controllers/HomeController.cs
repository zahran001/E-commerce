using E_commerce.Web.Models;
using E_commerce.Web.Service.IService;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;

namespace E_commerce.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductService _productService;

        public HomeController(IProductService productService)
        {
            _productService = productService;
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
