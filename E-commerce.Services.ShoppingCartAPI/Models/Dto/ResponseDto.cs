namespace E_commerce.Services.ShoppingCartAPI.Models.Dto
{
    public class ResponseDto
    {
        // need the object itself - it can be a Coupon, IEnumerable<Coupon>, etc.
        public object? Result { get; set; }
        public bool IsSuccess { get; set; } = true; // default value
        public string Message { get; set; } = "";
    }
}
