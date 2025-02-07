using E_commerce.Services.AuthAPI.Models.Dto;
using E_commerce.Services.AuthAPI.Service.IService;

namespace E_commerce.Services.AuthAPI.Service
{
	public class AuthService : IAuthService
	{
		public Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto)
		{
			throw new NotImplementedException();
		}

		public Task<UserDto> Register(RegistrationRequestDto registrationRequestDto)
		{
			throw new NotImplementedException();
		}
	}
}
