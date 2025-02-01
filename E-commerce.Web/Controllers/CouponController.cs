using E_commerce.Web.Models;
using E_commerce.Web.Service;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace E_commerce.Web.Controllers
    // Web Project -> MVC controller -> CouponController
{
    public class CouponController : Controller
    {
        // In order to invoke the CouponAPI, we have the CouponService.
        // Add that in the constructor.
        private readonly ICouponService _couponService;
        public CouponController(ICouponService couponService)
        {
            _couponService = couponService;
        }
        
        // Calling the CouponService
        // CouponService is asynchronous
        public async Task<IActionResult> CouponIndex()
        {
            List<CouponDto>? list = new();

            ResponseDto? response = await _couponService.GetAllCouponsAsync();

            if  (response != null && response.IsSuccess)
            {
                // ResponseDto.Result is an object, so we need to deserialize the object.
                // T obj = JsonConvert.DeserializeObject<T>(jsonString);

                list = JsonConvert.DeserializeObject<List<CouponDto>>(Convert.ToString(response.Result));
            }
            else
            {
                TempData["error"] = response?.Message; // null check
            }
            return View(list);	
		}
		// In CouponAPIController, the route is "api/coupon"

        public async Task<IActionResult> CreateCoupon()
        {
            return View();
        }
        // In ASP.NET MVC (and ASP.NET Core), action methods are treated as GET requests by default.

        [HttpPost]
        public async Task<IActionResult> CreateCoupon(CouponDto model)
        {
            // server-side validation
            if (ModelState.IsValid)
            {
                ResponseDto? response = await _couponService.CreateCouponAsync(model);

                if (response != null && response.IsSuccess)
                {
					TempData["success"] = "New Coupon Created";
					return RedirectToAction(nameof(CouponIndex));
                }
                else
                {
                    TempData["error"] = response?.Message; // null check
                }

            }
            return View(model);
        }

		// This method retrieves the coupon by its ID and displays the delete confirmation view.
		public async Task<IActionResult> DeleteCoupon(int couponId)
		{
			ResponseDto? response = await _couponService.GetCouponByIdAsync(couponId);

			if (response != null && response.IsSuccess)
			{
				CouponDto? model = JsonConvert.DeserializeObject<CouponDto>(Convert.ToString(response.Result));
				return View(model);
			}
            else
            {
                TempData["error"] = response?.Message; // null check
            }
            return NotFound();
		}


        [HttpPost]
        public async Task<IActionResult> DeleteCoupon(CouponDto couponDto)
        {
            ResponseDto? response = await _couponService.DeleteCouponAsync(couponDto.CouponId);

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Coupon Deleted";
				return RedirectToAction(nameof(CouponIndex));
            }
            else
            {
                TempData["error"] = response?.Message; // null check
            }

            return View(couponDto);
        }

    }
}

/*
* Debugging the delete endpoint:
* Used Breakpoint in the BaseService.
* Method Not Allowed 
* Didn't have the id in the route in CouponAPIController.
*/