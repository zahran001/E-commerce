using E_commerce.Services.AuthAPI.Models.Dto;

namespace E_commerce.Services.AuthAPI.Service.IService
{
	public interface IAuthService
	{
		Task<string> Register(RegistrationRequestDto registrationRequestDto); // When a user is registering, the return type would be UserDto.
		Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto); // When a user is logging in, the return type would be LoginResponseDto.
		Task<bool> AssignRole(string email, string roleName);
	}
}
