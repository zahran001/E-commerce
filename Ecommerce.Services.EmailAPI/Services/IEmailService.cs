using E_commerce.Web.EmailAPI.Models.Dto;

namespace Ecommerce.Services.EmailAPI.Services
{
    public interface IEmailService
    {
        Task EmailCartAndLog(CartDto cartDto);
    }
}
