using E_commerce.Services.AuthAPI.Models.Dto;
using E_commerce.Services.AuthAPI.Service.IService;
using Ecommerce.MessageBus;
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
		private readonly ILogger<AuthAPIController> _logger;
		// add the ResponseDto
		protected ResponseDto _response; // not accessible outside of the AuthAPIController class hierarchy
		private readonly IMessageBus _messageBus;
		private IConfiguration _configuration;

		public AuthAPIController(
			IAuthService authService,
			ILogger<AuthAPIController> logger,
			IMessageBus messageBus,
			IConfiguration configuration)
		{
			_authService = authService;
			_logger = logger;
			_response = new();
			_messageBus = messageBus;
			_configuration = configuration;
		}


		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegistrationRequestDto model)
		{
			_logger.LogInformation("Registration attempt for user {Email}", model.Email);

			var errorMessage = await _authService.Register(model);
			if (!string.IsNullOrEmpty(errorMessage))
			{
				_logger.LogWarning("Registration failed for {Email}: {Error}", model.Email, errorMessage);
				_response.IsSuccess = false;
				_response.Message = errorMessage;
				return BadRequest(_response);
			}

			_logger.LogInformation("User {Email} registered successfully", model.Email);

			await _messageBus.PublishMessage(model.Email, _configuration.GetValue<string>("TopicAndQueueNames:LogUserQueue"));

			return Ok(_response);
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
		{
			_logger.LogInformation("Login attempt for user {UserName}", model.UserName);

			var loginResponse = await _authService.Login(model);
			if (loginResponse.User == null)
			{
				_logger.LogWarning("Login failed for {UserName}: invalid credentials", model.UserName);
				_response.IsSuccess = false;
				_response.Message = "Username or password is incorrect";
				return BadRequest(_response);
			}

			_logger.LogInformation("Login attempt for user {UserName}", model.UserName);

			// if login is successful
			_response.Result = loginResponse;
			return Ok(_response);
		}

		[HttpPost("AssignRole")]
		public async Task<IActionResult> AssignRole([FromBody] RegistrationRequestDto model)
		{
			_logger.LogInformation("Assigning role {Role} to user {Email}", model.Role, model.Email);

			var AssignRoleIsSuccessful = await _authService.AssignRole(model.Email, model.Role.ToUpper());
			if (!AssignRoleIsSuccessful)
			{
				_logger.LogWarning("Failed to assign role {Role} to user {Email}", model.Role, model.Email);
				_response.IsSuccess = false;
				_response.Message = "Error configuring user role";
				return BadRequest(_response);
			}

			_logger.LogInformation("Successfully assigned role {Role} to user {Email}", model.Role, model.Email);

			// if login is successful
			_response.Result = AssignRoleIsSuccessful;
			return Ok(_response);
		}
	}
}
