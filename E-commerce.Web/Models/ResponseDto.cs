namespace E_commerce.Web.Models.Dto
{
    public class ResponseDto
    {
        // need the object itself - it can be a Coupon, IEnumerable<Coupon>, etc.
        public object? Result { get; set; }
        public bool IsSuccess { get; set; } = true; // default value
        public string Message { get; set; } = "";
    }
}

// Keeping the models isolated to their corresponding projects is a good practice.
