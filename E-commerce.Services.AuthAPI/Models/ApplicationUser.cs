using Microsoft.AspNetCore.Identity;

namespace E_commerce.Services.AuthAPI.Models
{
	public class ApplicationUser : IdentityUser
	{
		public string FirstName  { get; set; }
		public string LastName { get; set; }
		// Define the change in DbContext

	}
}
