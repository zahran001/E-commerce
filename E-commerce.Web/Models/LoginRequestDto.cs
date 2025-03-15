using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;

namespace E_commerce.Web.Models
{
	public class LoginRequestDto
	{
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Password { get; set; }
	}
}

// When a user successfully logs in, we will return a UserDTO containing the user's details. 
// Additionally, we will generate and return a token for authentication and authorization purposes.
