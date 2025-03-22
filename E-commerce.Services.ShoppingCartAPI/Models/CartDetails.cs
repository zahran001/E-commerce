using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using E_commerce.Services.ShoppingCartAPI.Models.Dto;

namespace E_commerce.Services.ShoppingCartAPI.Models
{
    public class CartDetails
    {
        [Key]
        public int CartDetailsId { get; set; }

        // Define the CartHeaderId property, which is a foreign key to the CartHeader table
        public int CartHeaderId { get; set; }

        // Specify that CartHeaderId is a foreign key linking to the CartHeader table
        [ForeignKey("CartHeaderId")]
        public CartHeader CartHeader { get; set; } // Navigation property to access the associated CartHeader

        public int ProductId { get; set; } 
        
        // Populate the product details by calling the ProductAPI
        
        [NotMapped]
        public ProductDto Product { get; set; } // Don't add that to the database

        public int Count { get; set; }

    }
}
