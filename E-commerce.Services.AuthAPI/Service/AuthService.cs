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

		public async Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto)
		{
			// We will get the username and password based on the LoginRequestDto

			// retrieve the user from database
			var user = _db.ApplicationUsers.FirstOrDefault(u=>u.UserName.ToLower()==loginRequestDto.UserName.ToLower());

			bool isValid = await _userManager.CheckPasswordAsync(user, loginRequestDto.Password);

			if(user==null || !isValid)
			{
				return new LoginResponseDto() { User = null, Token = "" };
			}

			/* Generate the JWT token if the user was found */

			// populate userDto based on the user
			UserDto userDto = new()
			{
				Email = user.Email,
				ID = user.Id,
				FirstName = user.FirstName,
				LastName = user.LastName,
				PhoneNumber = user.PhoneNumber
			};

			LoginResponseDto loginResponseDto = new LoginResponseDto()
			{
				User = userDto,
				Token = ""
			};

			return loginResponseDto;


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
