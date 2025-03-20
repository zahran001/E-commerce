﻿namespace E_commerce.Services.ShoppingCartAPI.Models.Dto
{
    public class CartDto
    {
        // one CartHeader and a list of CartDetails
        public CartHeaderDto CartHeader { get; set; }
        public IEnumerable<CartDetailsDto>? CartDetails { get; set; }
    }
}
