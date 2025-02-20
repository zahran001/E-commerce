using E_commerce.Services.AuthAPI.Models.Dto;
using E_commerce.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Services.AuthAPI.Controllers
{
	[Route("api/auth")]
	[ApiController]
	public class AuthAPIController : ControllerBase
	{
		// inject the AuthService
		private readonly IAuthService _authService;
		// add the ResponseDto
		protected ResponseDto _response; // not accessible outside of the AuthAPIController class hierarchy
		public AuthAPIController(IAuthService authService)
		{
			_authService = authService;
			_response = new();
		}


		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegistrationRequestDto model)
		{
			var errorMessage = await _authService.Register(model);
			if (!string.IsNullOrEmpty(errorMessage))
			{
				_response.IsSuccess = false;
				_response.Message = errorMessage;
				return BadRequest(_response);
			}

			return Ok(_response);
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
		{
			var loginResponse = await _authService.Login(model);
			if (loginResponse.User == null)
			{
				_response.IsSuccess = false;
				_response.Message = "Username or password is incorrect";
				return BadRequest(_response);
			}

			// if login is successful
			_response.Result = loginResponse;
			return Ok(_response);
		}

		[HttpPost("AssignRole")]
		public async Task<IActionResult> AssignRole([FromBody] RegistrationRequestDto model)
		{
			var AssignRoleIsSuccessful = await _authService.AssignRole(model.Email, model.Role.ToUpper());
			if (!AssignRoleIsSuccessful)
			{
				_response.IsSuccess = false;
				_response.Message = "Error configuring user role";
				return BadRequest(_response);
			}

			// if login is successful
			_response.Result = AssignRoleIsSuccessful;
			return Ok(_response);
		}
	}
}
