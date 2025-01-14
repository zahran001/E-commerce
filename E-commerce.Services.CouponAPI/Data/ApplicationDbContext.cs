using E_commerce.Services.CouponAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Services.CouponAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Constructor
        // The constructor accepts DbContextOptions of type ApplicationDbContext.
        // These options are passed to the base DbContext class to configure the context.
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSet
        // This is the table that we are going to create in the database.
        public DbSet<Coupon> Coupons { get; set; }

        // seed data
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Coupon>().HasData(new Coupon
            {
                CouponId = 1,
                CouponCode = "10OFF",
                DiscountAmount = 10,
                MinimumAmount = 100,
                LastUpdated = new DateTime(2025, 1, 1, 12, 0, 0) // January 1, 2025, at 12:00 PM
            });

            modelBuilder.Entity<Coupon>().HasData(new Coupon
            {
                CouponId = 2,
                CouponCode = "20OFF",
                DiscountAmount = 20,
                MinimumAmount = 90,
                LastUpdated = new DateTime(2025, 1, 4, 11, 0, 0) // January 4, 2025, at 11:00 PM
            });


        }
    }
}
