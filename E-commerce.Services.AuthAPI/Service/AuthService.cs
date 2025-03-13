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
		private readonly IJwtTokenGenerator _jwtTokenGenerator;

		// Inject helper methods
		public AuthService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, 
			RoleManager<IdentityRole> roleManager, IJwtTokenGenerator jwtTokenGenerator)
		{
			_db = db;
			_userManager = userManager;
			_roleManager = roleManager;
			_jwtTokenGenerator = jwtTokenGenerator;
		}

		public async Task<bool> AssignRole(string email, string roleName)
		{
			// RoleManager provides helper methods to create a role in the database

			// Retrieve user based on the email
			var user = _db.ApplicationUsers.FirstOrDefault(u => u.Email.ToLower() == email.ToLower());
			if (user != null)
			{
				// check whether the role exists in the database
				if (!_roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
				{
					// create the role if it does not exist
					_roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
				}
				await _userManager.AddToRoleAsync(user, roleName);
				return true;
			}
			return false;
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

			var roles = await _userManager.GetRolesAsync(user); // retrieve all the roles

			/* Generate the JWT token if the user was found */
			var token = _jwtTokenGenerator.GenerateToken(user, roles);

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
				Token = token
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
