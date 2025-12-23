using AutoMapper;
using E_commerce.Services.ProductAPI.Models;
using E_commerce.Services.ProductAPI.Data;
using E_commerce.Services.ProductAPI.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Services.ProductAPI.Controllers
{
    [Route("api/product")]
    [ApiController]
    public class ProductAPIController : ControllerBase
    {
        // retrieve all Products
        // In order to retrieve the records, we will be using EF Core, so we need ApplicationDbContext using DI.
        private readonly ApplicationDbContext _db;
        // return ResponseDto in the controller
        private ResponseDto _response;
        // inject AutoMapper in the controller
        private IMapper _mapper;
        // inject ILogger
        private readonly ILogger<ProductAPIController> _logger;



        // Constructor
        public ProductAPIController(ApplicationDbContext db, IMapper mapper, ILogger<ProductAPIController> logger)
        {
            _db = db;
            _response = new ResponseDto();
            _mapper = mapper;
            _logger = logger;
        }

        // get all Products
        [HttpGet]
        public ResponseDto Get()
        {
            _logger.LogInformation("Fetching all products");

            try
            {
                IEnumerable<Product> objList = _db.Products.ToList();
                _response.Result = _mapper.Map<IEnumerable<ProductDto>>(objList);
                _logger.LogInformation("Successfully retrieved {Count} products", objList.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all products");
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            // return back the response
            return _response;
        }

		// get Product by id
		[HttpGet]
        [Route("{id:int}")]
        public ResponseDto Get(int id)
        {
            _logger.LogInformation("Fetching product with ID {ProductId}", id);

            try
            {
                Product obj = _db.Products.First(u => u.ProductId == id);
                _response.Result = _mapper.Map<ProductDto>(obj);
                _logger.LogInformation("Successfully retrieved product {ProductId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product {ProductId}", id);
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }


		// Passing the object in the request body
		// create a new Product
		[HttpPost]
        [Authorize(Roles = "ADMIN")]
        public ResponseDto Post([FromBody] ProductDto ProductDto)
        {
            _logger.LogInformation("Creating new product: {ProductName}", ProductDto.Name);

            // convert ProductDto to Product to add to _db (database)

            try
            {
                // DestinationType destinationObject = _mapper.Map<DestinationType>(sourceObject);
                Product obj = _mapper.Map<Product>(ProductDto);
                _db.Products.Add(obj);
                _db.SaveChanges();

                // return the ProductDto
                _response.Result = _mapper.Map<ProductDto>(obj);
                _logger.LogInformation("Product created successfully with ID {ProductId}", obj.ProductId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product {ProductName}", ProductDto.Name);
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
 
        // update a Product
        [HttpPut]
		[Authorize(Roles = "ADMIN")]
		public ResponseDto Put([FromBody] ProductDto ProductDto)
        {
            _logger.LogInformation("Updating product {ProductId}: {ProductName}", ProductDto.ProductId, ProductDto.Name);

            // convert ProductDto to Product to add to _db (database)

            try
            {
                // DestinationType destinationObject = _mapper.Map<DestinationType>(sourceObject);
                Product obj = _mapper.Map<Product>(ProductDto);
                _db.Products.Update(obj); // EF Core - based on the id the obj, it will update the record
                _db.SaveChanges();

                // return the ProductDto
                _response.Result = _mapper.Map<ProductDto>(obj);
                _logger.LogInformation("Product {ProductId} updated successfully", ProductDto.ProductId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", ProductDto.ProductId);
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }


        // delete a Product
        [HttpDelete]
        [Route("{id:int}")]
		[Authorize(Roles = "ADMIN")]
		public ResponseDto Delete(int id)
        {
            _logger.LogInformation("Deleting product {ProductId}", id);

            try
            {
                // retrieve that Product
                Product obj = _db.Products.First(u=>u.ProductId == id);
                _db.Products.Remove(obj) ;
                _db.SaveChanges();
                _logger.LogInformation("Product {ProductId} deleted successfully", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", id);
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
        // Passing the ID in the URL

    }
}
// Architecture for the API response: Whenever multiple APIs are being consumed, the response will be in one object format.
// We want to have a common response for all the endpoints.

// We have added dtos in the project. We should not return Product or the data object itself - we should return the dto.
// To avoid a manual conversion - we can use AutoMapper for this.
