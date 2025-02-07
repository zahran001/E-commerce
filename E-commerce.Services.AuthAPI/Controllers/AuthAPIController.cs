using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Services.AuthAPI.Controllers
{
	[Route("api/auth")]
	[ApiController]
	public class AuthAPIController : ControllerBase
	{
		[HttpPost("register")]
		public async Task<IActionResult> Register()
		{
			return Ok();
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login()
		{
			return Ok();
		}
	}
}
