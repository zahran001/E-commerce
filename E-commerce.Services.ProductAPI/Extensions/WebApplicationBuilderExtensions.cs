using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace E_commerce.Services.ProductAPI.Extensions
{
	public static class WebApplicationBuilderExtensions
	{
		public static WebApplicationBuilder AddAppAuthentication(this WebApplicationBuilder builder)
		// The `static` keyword makes the method part of a static class,
		// The `this` specifies the type being extended, allowing the method to be called as if it were an instance method.
		{
			var settingsSection = builder.Configuration.GetSection("ApiSettings");

			var secret = settingsSection.GetValue<string>("Secret");
			var issuer = settingsSection.GetValue<string>("Issuer");
			var audience = settingsSection.GetValue<string>("Audience");

			// Extract the secret key
			var key = Encoding.ASCII.GetBytes(secret);

			builder.Services.AddAuthentication(x =>
			{
				x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			}).AddJwtBearer(x =>
			{
				x.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new SymmetricSecurityKey(key),
					ValidateIssuer = true,
					ValidIssuer = issuer,
					ValidAudience = audience,
					ValidateAudience = true,
				};
			});

			return builder;
		}
	}
}
