using E_commerce.Services.ProductAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Services.ProductAPI.Data
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
        public DbSet<Product> Products { get; set; }

        // seed data
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Product>().HasData(new Product
			{
				ProductId = 1,
				Name = "Apple - iPhone 16e 128GB - Apple Intelligence - White (AT&T)",
				Price = 599.00,
				Description = " Quisque vel lacus ac magna, vehicula sagittis ut non lacus.<br/> Vestibulum arcu turpis, maximus malesuada neque. Phasellus commodo cursus pretium.",
				ImageUrl = "https://placehold.co/603x403",
				CategoryName = "Cell Phone"
			});
			modelBuilder.Entity<Product>().HasData(new Product
			{
				ProductId = 2,
				Name = "Insignia™ - 65\" Class F50 Series LED 4K UHD Smart Fire TV",
				Price = 413.99,
				Description = " Quisque vel lacus ac magna, vehicula sagittis ut non lacus.<br/> Vestibulum arcu turpis, maximus malesuada neque. Phasellus commodo cursus pretium.",
				ImageUrl = "https://placehold.co/602x402",
				CategoryName = "TV & Home Theater"
			});
			modelBuilder.Entity<Product>().HasData(new Product
			{
				ProductId = 3,
				Name = "Soundcore - by Anker Liberty 4 NC Noise Canceling True Wireless Earbud Headphones - Black",
				Price = 70.00,
				Description = " Quisque vel lacus ac magna, vehicula sagittis ut non lacus.<br/> Vestibulum arcu turpis, maximus malesuada neque. Phasellus commodo cursus pretium.",
				ImageUrl = "https://placehold.co/601x401",
				CategoryName = "Audio & Headphones"
			});
			modelBuilder.Entity<Product>().HasData(new Product
			{
				ProductId = 4,
				Name = "Garmin - Forerunner 265 GPS Smartwatch 46 mm Fiber-Reinforced polymer - Black",
				Price = 399.99,
				Description = " Quisque vel lacus ac magna, vehicula sagittis ut non lacus.<br/> Vestibulum arcu turpis, maximus malesuada neque. Phasellus commodo cursus pretium.",
				ImageUrl = "https://placehold.co/600x400",
				CategoryName = "Wearable Technology"
			});


		}
    }
}
