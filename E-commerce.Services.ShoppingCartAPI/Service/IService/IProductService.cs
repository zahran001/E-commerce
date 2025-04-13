using E_commerce.Services.ShoppingCartAPI.Models.Dto;

namespace E_commerce.Services.ShoppingCartAPI.Service.IService
{
    // Load all products from the ProductAPI
    public interface IProductService
    {
        Task<IEnumerable<ProductDto>> GetProducts();
        
    }
}
