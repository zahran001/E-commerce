namespace E_commerce.Services.EmailAPI.Models.Dto
{
    public class CartDto
    {
        // one CartHeader and a list of CartDetails
        public CartHeaderDto CartHeader { get; set; }
        public IEnumerable<CartDetailsDto>? CartDetails { get; set; }
    }
}
