using System.ComponentModel.DataAnnotations;

namespace E_commerce.Web.Models
{
	public class RegistrationRequestDto
	{
		[Required]
		public string Email { get; set; }
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        public string PhoneNumber { get; set; }
        [Required]
        public string Password { get; set; }
		public string? Role { get; set; }
	}
}
