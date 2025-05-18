namespace Ecommerce.Services.EmailAPI.Models
{
    public class EmailLogger
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public DateTime? EmailSent { get; set; }
    }
}
