using E_commerce.Services.ProductAPI.Models.Dto;
using E_commerce.Services.ProductAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Services.ProductAPI.Controllers
{
    [Route("api/product")]
    [ApiController]
    public class ProductAPIController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductAPIController> _logger;
        protected ResponseDto _response;

        public ProductAPIController(
            IProductService productService,
            ILogger<ProductAPIController> logger)
        {
            _productService = productService;
            _logger = logger;
            _response = new ResponseDto();
        }

        // get all Products
        [HttpGet]
        public async Task<ActionResult<ResponseDto>> GetAll()
        {
            try
            {
                _logger.LogInformation("Fetching all products");
                var products = await _productService.GetAllProductsAsync();

                _response.Result = products;
                _response.IsSuccess = true;
                _response.Message = $"Retrieved {products.Count} products";

                return Ok(_response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all products");
                _response.IsSuccess = false;
                _response.Message = "Error retrieving products";
                return BadRequest(_response);
            }
        }

		// get Product by id
		[HttpGet]
        [Route("{id:int}")]
        public async Task<ActionResult<ResponseDto>> GetById([FromRoute] int id)
        {
            try
            {
                _logger.LogInformation("Fetching product with ID: {ProductId}", id);
                var product = await _productService.GetProductByIdAsync(id);

                if (product == null)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Product not found";
                    return NotFound(_response);
                }

                _response.Result = product;
                _response.IsSuccess = true;
                _response.Message = "Product retrieved successfully";

                return Ok(_response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product with ID: {ProductId}", id);
                _response.IsSuccess = false;
                _response.Message = "Error retrieving product";
                return BadRequest(_response);
            }
        }


		// Passing the object in the request body
		// create a new Product
		[HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult<ResponseDto>> Post([FromBody] ProductDto productDto)
        {
            try
            {
                _logger.LogInformation("Creating new product: {ProductName}", productDto.Name);
                var createdProduct = await _productService.CreateProductAsync(productDto);

                _response.Result = createdProduct;
                _response.IsSuccess = true;
                _response.Message = "Product created successfully";

                return CreatedAtAction(nameof(GetById), new { id = createdProduct.ProductId }, _response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                _response.IsSuccess = false;
                _response.Message = "Error creating product";
                return BadRequest(_response);
            }
        }
 
        // update a Product
        [HttpPut]
		[Authorize(Roles = "ADMIN")]
		public async Task<ActionResult<ResponseDto>> Put([FromBody] ProductDto productDto)
        {
            try
            {
                _logger.LogInformation("Updating product with ID: {ProductId}", productDto.ProductId);
                var success = await _productService.UpdateProductAsync(productDto);

                if (!success)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Product not found";
                    return NotFound(_response);
                }

                _response.IsSuccess = true;
                _response.Message = "Product updated successfully";

                return Ok(_response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product with ID: {ProductId}", productDto.ProductId);
                _response.IsSuccess = false;
                _response.Message = "Error updating product";
                return BadRequest(_response);
            }
        }


        // delete a Product
        [HttpDelete("{id:int}")]
		[Authorize(Roles = "ADMIN")]
		public async Task<ActionResult<ResponseDto>> Delete([FromRoute] int id)
        {
            try
            {
                _logger.LogInformation("Deleting product with ID: {ProductId}", id);
                var success = await _productService.DeleteProductAsync(id);

                if (!success)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Product not found";
                    return NotFound(_response);
                }

                _response.IsSuccess = true;
                _response.Message = "Product deleted successfully";

                return Ok(_response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product with ID: {ProductId}", id);
                _response.IsSuccess = false;
                _response.Message = "Error deleting product";
                return BadRequest(_response);
            }
        }
    }
}
