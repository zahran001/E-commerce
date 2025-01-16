using E_commerce.Services.CouponAPI.Data;
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

        // Constructor
        public CouponAPIController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public object Get()
        {
            try
            {
                IEnumerable<Coupon> objList = _db.Coupons.ToList();
                return objList;
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving coupons.", error = ex.Message });
            }
        }

        // get coupon by id
        [HttpGet]
        [Route("{id:int}")]
        public object Get(int id)
        {
            try
            {
                Coupon obj = _db.Coupons.First(u=>u.CouponId == id);
                return obj;
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving coupons.", error = ex.Message });
            }
        }

    }
}