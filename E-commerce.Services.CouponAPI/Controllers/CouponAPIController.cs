using AutoMapper;
using E_commerce.Services.CouponAPI.Data;
using E_commerce.Services.CouponAPI.Models;
using E_commerce.Services.CouponAPI.Models.Dto;
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
        // return ResponseDto in the controller
        private ResponseDto _response;
        // inject AutoMapper in the controller
        private IMapper _mapper;

        

        // Constructor
        public CouponAPIController(ApplicationDbContext db, IMapper mapper)
        {
            _db = db;
            _response = new ResponseDto();
            _mapper = mapper;
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
                CouponDto couponDto = new CouponDto()
                {
                    CouponId = obj.CouponId,
                    CouponCode = obj.CouponCode,
                    DiscountAmount = obj.DiscountAmount,
                    MinimumAmount = obj.MinimumAmount,
                };
                _response.Result = couponDto;
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

// We have added dtos in the project. We should not return Coupon or the data object itself - we should return the dto.
// To avoid a manual conversion - we can use AutoMapper for this.
