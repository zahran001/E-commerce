using E_commerce.Web.Models;

namespace E_commerce.Web.Service.IService
{
    public interface IProductService
    {
        Task<ResponseDto?> GetAllProductsAsync();
		Task<ResponseDto?> GetProductByIdAsync(int id);
		Task<ResponseDto?> CreateProductAsync(ProductDto productDto);
        Task<ResponseDto?> UpdateProductAsync(ProductDto productDto);
        Task<ResponseDto?> DeleteProductAsync(int id);

    }
}

// When we are working with the product service, we need ProductDto.
// We are getting all the products - type of that model will be ProductDto.
