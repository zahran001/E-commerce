using E_commerce.Services.EmailAPI.Models.Dto;

namespace Ecommerce.Services.EmailAPI.Services
{
    public interface IEmailService
    {
        Task EmailCartAndLog(CartDto cartDto);
    }
}
