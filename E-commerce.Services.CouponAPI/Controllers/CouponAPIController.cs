using E_commerce.Services.CouponAPI.Data;
using E_commerce.Services.CouponAPI.Dto;
using E_commerce.Services.CouponAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Services.CouponAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CouponAPIController : ControllerBase
    {
        // retrieve all coupons
        // In order to retrieve the records, we will be using EF Core, so we need ApplicationDbContext using DI.
        private readonly ApplicationDbContext _db;
        //return ResponseDto in the controller
        private ResponseDto _response;

        

        // Constructor
        public CouponAPIController(ApplicationDbContext db)
        {
            _db = db;
            _response = new ResponseDto(); // initialize
        }

        [HttpGet]
        public ResponseDto Get()
        {
            try
            {
                IEnumerable<Coupon> objList = _db.Coupons.ToList();
                _response.Result = objList;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            // return back the response
            return _response;
        }

        // get coupon by id
        [HttpGet]
        [Route("{id:int}")]
        public ResponseDto Get(int id)
        {
            try
            {
                Coupon obj = _db.Coupons.First(u=>u.CouponId == id);
                _response.Result = obj;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

    }
}
// Architecture for the API response: Whenever multiple APIs are being consumed, the response will be in one object format.
// We want to have a common response for all the endpoints.