using E_commerce.Services.ProductAPI.Models.Dto;

namespace E_commerce.Services.ProductAPI.Service.IService
{
    public interface IProductService
    {
        /// <summary>
        /// Retrieves all products with caching
        /// </summary>
        /// <returns>List of all product DTOs</returns>
        Task<List<ProductDto>> GetAllProductsAsync();

        /// <summary>
        /// Retrieves a single product by ID with caching
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>Product DTO or null if not found</returns>
        Task<ProductDto> GetProductByIdAsync(int id);

        /// <summary>
        /// Creates a new product and invalidates cache
        /// </summary>
        /// <param name="productDto">Product data</param>
        /// <returns>Created product DTO with generated ID</returns>
        Task<ProductDto> CreateProductAsync(ProductDto productDto);

        /// <summary>
        /// Updates an existing product and invalidates cache
        /// </summary>
        /// <param name="productDto">Updated product data</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateProductAsync(ProductDto productDto);

        /// <summary>
        /// Deletes a product and invalidates cache
        /// </summary>
        /// <param name="id">Product ID to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteProductAsync(int id);
    }
}
