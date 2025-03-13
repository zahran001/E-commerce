using E_commerce.Web.Service.IService;
using E_commerce.Web.Utility;
using Newtonsoft.Json.Linq;

namespace E_commerce.Web.Service
{
	public class TokenProvider : ITokenProvider
	{
		// Inject HttpContextAccessor to work with the cookies
		private readonly IHttpContextAccessor _contextAccessor;
		public TokenProvider(IHttpContextAccessor contextAccessor)
		{
			_contextAccessor = contextAccessor;
		}
		public void ClearToken()
		{
			_contextAccessor.HttpContext?.Response.Cookies.Delete(StaticDetails.TokenCookie); // key name
		}

		public string? GetToken()
		{
			// retrieve the token
			string? token = null;
			bool? hasToken = _contextAccessor.HttpContext.Request.Cookies.TryGetValue(StaticDetails.TokenCookie, out token);
			return hasToken is true ? token : null; // Explicitly check for true to prevent a null reference error

		}

		public void SetToken(string token)
		{
			_contextAccessor.HttpContext?.Response.Cookies.Append(StaticDetails.TokenCookie, token);
		}
	}
}
