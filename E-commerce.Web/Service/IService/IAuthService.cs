using E_commerce.Web.Models;

namespace E_commerce.Web.Service.IService
{
	public interface IAuthService
	{
		Task<ResponseDto?> LoginAsync(LoginRequestDto loginRequestDto);
		Task<ResponseDto?> RegisterAsync(RegistrationRequestDto registrationRequestDto);
		Task<ResponseDto?> AssignRoleAsync(RegistrationRequestDto registrationRequestDto);
	}
}
