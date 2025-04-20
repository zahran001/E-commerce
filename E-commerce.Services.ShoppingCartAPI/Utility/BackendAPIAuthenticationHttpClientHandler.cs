using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace E_commerce.Services.ShoppingCartAPI.Utility
{
    public class BackendAPIAuthenticationHttpClientHandler : DelegatingHandler
    {
        // retrieven Bearer token from the context accessor
        private readonly IHttpContextAccessor _accessor;
        public BackendAPIAuthenticationHttpClientHandler(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        // access the token and add that authorization header
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await _accessor.HttpContext.GetTokenAsync("access_token");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return await base.SendAsync(request, cancellationToken);
        }
    }

}
