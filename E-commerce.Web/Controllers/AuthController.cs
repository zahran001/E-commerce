using E_commerce.Web.Models;
using E_commerce.Web.Service.IService;
using E_commerce.Web.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;

namespace E_commerce.Web.Controllers
{
	public class AuthController : Controller
	{
		private readonly IAuthService _authService;
		public AuthController(IAuthService authService)
		{
			_authService = authService;
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

		public IActionResult Logout()
        {
			return View();
        }

    }
}
