using E_commerce.Web.Models;
using E_commerce.Web.Service.IService;
using E_commerce.Web.Utility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace E_commerce.Web.Controllers
{
	public class AuthController : Controller
	{
		private readonly IAuthService _authService;
		private readonly ITokenProvider _tokenProvider;
		public AuthController(IAuthService authService, ITokenProvider tokenProvider)
		{
			_authService = authService;
			_tokenProvider = tokenProvider;
		}

		[HttpGet]
		public IActionResult Login()
		{
			LoginRequestDto loginRequestDto = new();
			return View(loginRequestDto);
		}

		
		[HttpPost]
		public async Task<IActionResult> Login(LoginRequestDto obj)
		{
			// Invoke the authService when this obj is retrieved
			ResponseDto responseDto = await _authService.LoginAsync(obj);

			// If it's successful, deserialize that 

			if (responseDto != null && responseDto.IsSuccess)
			{
				LoginResponseDto loginResponseDto = JsonConvert.DeserializeObject<LoginResponseDto>(Convert.ToString(responseDto.Result));

				await SignInUser(loginResponseDto);
				_tokenProvider.SetToken(loginResponseDto.Token);
				return RedirectToAction("Index", "Home");
			}
			else
			{
				ModelState.AddModelError("CustomError", responseDto.Message);
				return View(obj);
			}

		}
		

		[HttpGet]
		public IActionResult Register()
		{
			// Dropdown Population: SelectListItem is used to create a list of options for an HTML <select> dropdown in the registration form.
				
			var roleList = new List<SelectListItem>()
			{
				new SelectListItem{Text=StaticDetails.RoleAdmin, Value=StaticDetails.RoleAdmin},
				new SelectListItem{Text=StaticDetails.RoleCustomer, Value=StaticDetails.RoleCustomer},
			};

			// Pass roleList to the View
			ViewBag.RoleList = roleList;
			return View();
		}

		[HttpPost]
		public async Task<IActionResult> Register(RegistrationRequestDto obj)
		{
			// Invoke the authService when this obj is retrieved
			ResponseDto result = await _authService.RegisterAsync(obj);
			ResponseDto assignRole;

			if(result!=null && result.IsSuccess)
			{
				if (string.IsNullOrEmpty(obj.Role))
				{
					obj.Role = StaticDetails.RoleCustomer;
				}
				assignRole = await _authService.AssignRoleAsync(obj);
				if (assignRole!=null && assignRole.IsSuccess)
				{
					TempData["success"] = "Registration Successful";
					return RedirectToAction(nameof(Login));
				}
			}

			var roleList = new List<SelectListItem>()
			{
				new SelectListItem{Text=StaticDetails.RoleAdmin, Value=StaticDetails.RoleAdmin},
				new SelectListItem{Text=StaticDetails.RoleCustomer, Value=StaticDetails.RoleCustomer},
			};

			// Pass roleList to the View
			ViewBag.RoleList = roleList;
			return View(obj); // The Register view is reloaded with the user's entered details and an intact role selection dropdown
		}

		public async Task<IActionResult> Logout()
        {
			await HttpContext.SignOutAsync();
			_tokenProvider.ClearToken();
			return RedirectToAction("Index", "Home");
        }

		// Sign in a user using .NET Identity
		public async Task SignInUser(LoginResponseDto model)
		{
			var handler = new JwtSecurityTokenHandler();
			var jwt = handler.ReadJwtToken(model.Token); // read the token
			var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);

			// Extract and add relevant claims from the token
			identity.AddClaim(new Claim(JwtRegisteredClaimNames.Email, 
				jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Email).Value));

			identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub,
				jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Sub).Value));

			identity.AddClaim(new Claim(JwtRegisteredClaimNames.Name,
				jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Name).Value));

			identity.AddClaim(new Claim(ClaimTypes.Name,
				jwt.Claims.FirstOrDefault(u => u.Type == JwtRegisteredClaimNames.Email).Value));

			identity.AddClaim(new Claim(ClaimTypes.Role,
				jwt.Claims.FirstOrDefault(u => u.Type == "role").Value));

			// Create a ClaimsPrincipal from the extracted claims and sign in the user
			var principal = new ClaimsPrincipal(identity);
			await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
		}

    }
}
