namespace E_commerce.Web.Models
{
    public class CartDto
    {
        // one CartHeader and a list of CartDetails
        public CartHeaderDto CartHeader { get; set; }
        public IEnumerable<CartDetailsDto>? CartDetails { get; set; }
    }
}
