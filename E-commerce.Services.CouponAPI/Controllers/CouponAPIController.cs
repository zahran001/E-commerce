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

        // get all coupons
        [HttpGet]
        public ResponseDto Get()
        {
            try
            {
                IEnumerable<Coupon> objList = _db.Coupons.ToList();
                _response.Result = _mapper.Map<IEnumerable<CouponDto>>(objList);
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
                _response.Result = _mapper.Map<CouponDto>(obj);

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        // get coupon by code
        [HttpGet]
        [Route("GetByCode/{code}")]
        public ResponseDto GetByCode(string code)
        {
            try
            {
                Coupon obj = _db.Coupons.FirstOrDefault(u => u.CouponCode.ToLower() == code.ToLower());
                if (obj == null)
                {
                    _response.IsSuccess = false;
                }
                _response.Result = _mapper.Map<CouponDto>(obj);

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }


        // Passing the object in the request body
        // create a new coupon
        [HttpPost]
        public ResponseDto Post([FromBody] CouponDto couponDto)
        {
            // convert couponDto to Coupon to add to _db (database)

            try
            {
                // DestinationType destinationObject = _mapper.Map<DestinationType>(sourceObject);
                Coupon obj = _mapper.Map<Coupon>(couponDto);
                _db.Coupons.Add(obj);
                _db.SaveChanges();

                // return the couponDto
                _response.Result = _mapper.Map<CouponDto>(obj);

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
        // Testing
        // When the CouponId is set to 0, Entity Framework treats it as a new entity and assigns a new value to the primary key when the entity is saved to the database.

        // update a coupon
        [HttpPut]
        public ResponseDto Put([FromBody] CouponDto couponDto)
        {
            // convert couponDto to Coupon to add to _db (database)

            try
            {
                // DestinationType destinationObject = _mapper.Map<DestinationType>(sourceObject);
                Coupon obj = _mapper.Map<Coupon>(couponDto);
                _db.Coupons.Update(obj); // EF Core - based on the id the obj, it will update the record
                _db.SaveChanges();

                // return the couponDto
                _response.Result = _mapper.Map<CouponDto>(obj);

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        
        // delte a coupon
        [HttpDelete]
        public ResponseDto Delete(int id)
        {

            try
            {
                // retrieve that coupon
                Coupon obj = _db.Coupons.First(u=>u.CouponId == id);
                _db.Coupons.Remove(obj) ;
                _db.SaveChanges();

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
        // Passing the ID in the URL

    }
}
// Architecture for the API response: Whenever multiple APIs are being consumed, the response will be in one object format.
// We want to have a common response for all the endpoints.

// We have added dtos in the project. We should not return Coupon or the data object itself - we should return the dto.
// To avoid a manual conversion - we can use AutoMapper for this.
