namespace E_commerce.Services.AuthAPI.Models.Dto
{
	public class UserDto
	{ 
		public string ID { get; set; } // In .NET Identity - ID is a GUID - that's why, it will be a string.
		public string Email { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string PhoneNumber { get; set; }
	}
}
