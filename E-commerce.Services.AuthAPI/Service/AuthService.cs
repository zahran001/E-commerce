using E_commerce.Services.AuthAPI.Data;
using E_commerce.Services.AuthAPI.Models;
using E_commerce.Services.AuthAPI.Models.Dto;
using E_commerce.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.Identity;

namespace E_commerce.Services.AuthAPI.Service
{
	public class AuthService : IAuthService
	{
		// When we register or login an user, we will be updating the database - so, we need the ApplicationDbContext
		private readonly ApplicationDbContext _db;

		private readonly UserManager<ApplicationUser> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;

		// Inject helper methods
		public AuthService(ApplicationDbContext db,
			UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
		{
			_db = db;
			_userManager = userManager;
			_roleManager = roleManager;
		}

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
