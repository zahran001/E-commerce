using E_commerce.Web.Models;
using E_commerce.Web.Service.IService;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace E_commerce.Web.Controllers
// Web Project -> MVC controller -> ProductController
{
    public class ProductController : Controller
    {
        // In order to invoke the ProductAPI, we have the ProductService.
        // Add that in the constructor.
        private readonly IProductService _productService;
        public ProductController(IProductService productService)
        {
            _productService = productService;
        }
        
        // Calling the ProductService
        // ProductService is asynchronous
        public async Task<IActionResult> ProductIndex()
        {
            List<ProductDto>? list = new();

            ResponseDto? response = await _productService.GetAllProductsAsync();

            if  (response != null && response.IsSuccess)
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
		// In ProductAPIController, the route is "api/product"

        public async Task<IActionResult> CreateProduct()
        {
            return View();
        }
        // In ASP.NET MVC (and ASP.NET Core), action methods are treated as GET requests by default.

        [HttpPost]
        public async Task<IActionResult> CreateProduct(ProductDto model)
        {
            // server-side validation
            if (ModelState.IsValid)
            {
                ResponseDto? response = await _productService.CreateProductAsync(model);

                if (response != null && response.IsSuccess)
                {
					TempData["success"] = "New Product Created";
					return RedirectToAction(nameof(ProductIndex));
                }
                else
                {
                    TempData["error"] = response?.Message; // null check
                }

            }
            return View(model);
        }

		// This method retrieves the product by its ID and displays the delete confirmation view.
		public async Task<IActionResult> DeleteProduct(int productId)
		{
			ResponseDto? response = await _productService.GetProductByIdAsync(productId);

			if (response != null && response.IsSuccess)
			{
				ProductDto? model = JsonConvert.DeserializeObject<ProductDto>(Convert.ToString(response.Result));
				return View(model);
			}
            else
            {
                TempData["error"] = response?.Message; // null check
            }
            return NotFound();
		}


        [HttpPost]
        public async Task<IActionResult> DeleteProduct(ProductDto productDto)
        {
            ResponseDto? response = await _productService.DeleteProductAsync(productDto.ProductId);

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Product Deleted";
				return RedirectToAction(nameof(ProductIndex));
            }
            else
            {
                TempData["error"] = response?.Message; // null check
            }

            return View(productDto);
        }

    }
}