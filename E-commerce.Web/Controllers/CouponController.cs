using E_commerce.Web.Models;
using E_commerce.Web.Service;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

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
            return View(list);	
		}
		// In CouponAPIController, the route is "api/coupon"
	} 
}
