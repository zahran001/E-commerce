using System.ComponentModel.DataAnnotations;

namespace E_commerce.Services.CouponAPI.Models
{
    public class Coupon
    {
        [Key]
        public int CouponId { get; set; }
        [Required]
        public string CouponCode { get; set; }
        [Required]
        public double DiscountAmount { get; set; }
        [Required]
        public int MinimumAmount { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
