using E_commerce.Services.AuthAPI.Data;
using E_commerce.Services.AuthAPI.Models;
using E_commerce.Services.AuthAPI.Models.Dto;
using E_commerce.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.Identity;

namespace E_commerce.Services.AuthAPI.Service
{
	public class AuthService : IAuthService
	{
		// When a user registers or logs in, we update the database, so we need ApplicationDbContext.
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

		public async Task<string> Register(RegistrationRequestDto registrationRequestDto)
		{
			// Create a new user
			ApplicationUser user = new()
			{
				UserName = registrationRequestDto.Email,
				Email = registrationRequestDto.Email,
				NormalizedEmail = registrationRequestDto.Email.ToUpper(),
				FirstName = registrationRequestDto.FirstName,
				LastName = registrationRequestDto.LastName,
				PhoneNumber = registrationRequestDto.PhoneNumber,
			};

			try
			{
				// Using the helper method in UserManager
				var result = await _userManager.CreateAsync(user,registrationRequestDto.Password);

				if (result.Succeeded)
				{
					// retrieve the user
					var userToReturn = _db.ApplicationUsers.First(u=>u.UserName==registrationRequestDto.Email);

					// populate userDto based on the user
					UserDto userDto = new()
					{
						Email = userToReturn.Email,
						ID = userToReturn.Id,
						FirstName = userToReturn.FirstName,
						LastName = userToReturn.LastName,
						PhoneNumber = userToReturn.PhoneNumber
					};

					return "";

				}
				else
				{
					return result.Errors.FirstOrDefault().Description;
				}

			}
			catch (Exception ex) 
			{ 

			}
			return "Error Registering the User";

			
		}
	}
}
