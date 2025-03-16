using E_commerce.Web.Models;
using E_commerce.Web.Service.IService;
using E_commerce.Web.Utility;

namespace E_commerce.Web.Service
{
	public class AuthService : IAuthService
	{
		private readonly IBaseService _baseService;
		public AuthService(IBaseService baseService)
		{
			_baseService = baseService;
		}
		public async Task<ResponseDto?> AssignRoleAsync(RegistrationRequestDto registrationRequestDto)
		{
			return await _baseService.SendAsync(new RequestDto()
			{
				ApiType = StaticDetails.ApiType.POST,
				Data = registrationRequestDto,
				Url = StaticDetails.AuthApiBase + "/api/auth/AssignRole"
			});
		}

		public async Task<ResponseDto?> LoginAsync(LoginRequestDto loginRequestDto)
		{
			return await _baseService.SendAsync(new RequestDto()
			{
				ApiType = StaticDetails.ApiType.POST,
				Data = loginRequestDto,
				Url = StaticDetails.AuthApiBase + "/api/auth/login"
			}, withBearer: false);
		}

		public async Task<ResponseDto?> RegisterAsync(RegistrationRequestDto registrationRequestDto)
		{
			return await _baseService.SendAsync(new RequestDto()
			{
				ApiType = StaticDetails.ApiType.POST,
				Data = registrationRequestDto,
				Url = StaticDetails.AuthApiBase + "/api/auth/register"
			}, withBearer: false);
		}
	}
}
