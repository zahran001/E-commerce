using E_commerce.Services.ShoppingCartAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Services.ShoppingCartAPI.Data
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
        public DbSet<CartHeader> CartHeaders { get; set; }
        public DbSet<CartDetails> CartDetails { get; set; }
    }
}
