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
				Name = "Samosa",
				Price = 15,
				Description = " Quisque vel lacus ac magna, vehicula sagittis ut non lacus.<br/> Vestibulum arcu turpis, maximus malesuada neque. Phasellus commodo cursus pretium.",
				ImageUrl = "https://placehold.co/603x403",
				CategoryName = "Appetizer"
			});
			modelBuilder.Entity<Product>().HasData(new Product
			{
				ProductId = 2,
				Name = "Paneer Tikka",
				Price = 13.99,
				Description = " Quisque vel lacus ac magna, vehicula sagittis ut non lacus.<br/> Vestibulum arcu turpis, maximus malesuada neque. Phasellus commodo cursus pretium.",
				ImageUrl = "https://placehold.co/602x402",
				CategoryName = "Appetizer"
			});
			modelBuilder.Entity<Product>().HasData(new Product
			{
				ProductId = 3,
				Name = "Sweet Pie",
				Price = 10.99,
				Description = " Quisque vel lacus ac magna, vehicula sagittis ut non lacus.<br/> Vestibulum arcu turpis, maximus malesuada neque. Phasellus commodo cursus pretium.",
				ImageUrl = "https://placehold.co/601x401",
				CategoryName = "Dessert"
			});
			modelBuilder.Entity<Product>().HasData(new Product
			{
				ProductId = 4,
				Name = "Pav Bhaji",
				Price = 15,
				Description = " Quisque vel lacus ac magna, vehicula sagittis ut non lacus.<br/> Vestibulum arcu turpis, maximus malesuada neque. Phasellus commodo cursus pretium.",
				ImageUrl = "https://placehold.co/600x400",
				CategoryName = "Entree"
			});


		}
    }
}
