using E_commerce.Services.AuthAPI.Models;

namespace E_commerce.Services.AuthAPI.Service.IService
{
	public interface IJwtTokenGenerator
	{
		string GenerateToken(ApplicationUser applicationUser);
	}
}
