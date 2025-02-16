using E_commerce.Services.AuthAPI.Models;
using E_commerce.Services.AuthAPI.Service.IService;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace E_commerce.Services.AuthAPI.Service
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtOptions _jwtOptions;
        public JwtTokenGenerator(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }
        public string GenerateToken(ApplicationUser applicationUser)
        {
            // Generate token based on the applicationUser
            var tokenHandler = new JwtSecurityTokenHandler();

            // Extract the secret key
            var key = Encoding.ASCII.GetBytes(_jwtOptions.Secret);

            // Claims
            var claimList = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Email, applicationUser.Email),
                new Claim(JwtRegisteredClaimNames.Sub, applicationUser.Id),
                new Claim(JwtRegisteredClaimNames.Name, applicationUser.UserName.ToString())
            };

            // Token Descriptor - configuration properties for the token
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Audience = _jwtOptions.Audience,
                Issuer = _jwtOptions.Issuer,
                Subject = new ClaimsIdentity(claimList),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}

/*
    When we generate the token, we will be configuring it with Secret, Issuer, and Audience.
    For that we will require JwtOptions - configured that in the Program.cs file.
    We can retrive that inside the service here, in constructor, using DI.
*/

/*
    Debugging: JwtOptions
    JwtOptions Was Not Registered as a Service.
    The JwtOptions class is a configuration model, not a service.
    Dependency Injection (DI) does not automatically resolve configuration objects unless they are explicitly registered using IOptions<T>.
*/