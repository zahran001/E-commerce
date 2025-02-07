using System.Diagnostics.Contracts;

namespace E_commerce.Services.AuthAPI.Models.Dto
{
	public class LoginRequestDto
	{
		public string UserName { get; set; }
		public string Password { get; set; }
	}
}

// When a user successfully logs in, we will return a UserDTO containing the user's details. 
// Additionally, we will generate and return a token for authentication and authorization purposes.
