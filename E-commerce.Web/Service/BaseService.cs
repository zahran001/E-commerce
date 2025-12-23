using E_commerce.Web.Models;
using E_commerce.Web.Service.IService;
using Microsoft.AspNetCore.Diagnostics;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using static E_commerce.Web.Utility.StaticDetails;
using static System.Net.WebRequestMethods;

namespace E_commerce.Web.Service
{
    public class BaseService : IBaseService
    {
        // To make API calls, we need an HttpClient object. We can create an HttpClient object using the IHttpClientFactory service.
        private readonly IHttpClientFactory _httpClientFactory;
        public readonly ITokenProvider _tokenProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public BaseService(IHttpClientFactory httpClientFactory, ITokenProvider tokenProvider, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _tokenProvider = tokenProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ResponseDto?> SendAsync(RequestDto requestDto, bool withBearer = true)
        {
            HttpClient client = _httpClientFactory.CreateClient("EcommerceAPI");
            HttpRequestMessage message = new();
            message.Headers.Add("Accept", "application/json");
            // Token
            if (withBearer)
            {
                // Retrieve the token
                var token = _tokenProvider.GetToken();
                message.Headers.Add("Authorization", $"Bearer {token}");
            }

            // Propagate Correlation ID from Web MVC to downstream APIs
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("CorrelationId", out var correlationId) == true)
            {
                message.Headers.Add("X-Correlation-ID", correlationId.ToString());
            }

            message.RequestUri = new Uri(requestDto.Url);
            // If this is a GET request, we are done here.

            // If this is a POST or PUT request, we need to serialize the data that we receive in requestDto.Data and add that to message.Content like below.
            if (requestDto.Data != null)
            {
                message.Content = new StringContent(JsonConvert.SerializeObject(requestDto.Data), Encoding.UTF8, "application/json");
                // prepares the HTTP request message to send the JSON data to the specified URL
            }

            HttpResponseMessage? apiResponse = null;

            switch (requestDto.ApiType)
            {
                case ApiType.POST:
                    message.Method = HttpMethod.Post;
                    break;


                case ApiType.DELETE:
                    message.Method = HttpMethod.Delete;
                    break;

                case ApiType.PUT:
                    message.Method = HttpMethod.Put;
                    break;

                default:
                    message.Method = HttpMethod.Get;
                    break;

            }

            apiResponse = await client.SendAsync(message);

            // When we get the response back, we know from the API - we will be getting a ResponseDto object.

            try
            {
                switch (apiResponse.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        return new() { IsSuccess = false, Message = "Not Found" }; // return a new ResponseDto object with IsSuccess set to false and Message set to "Not Found"
                    case HttpStatusCode.Forbidden:
                        return new() { IsSuccess = false, Message = "Forbidden" };
                    case HttpStatusCode.Unauthorized:
                        return new() { IsSuccess = false, Message = "Unauthorized" };
                    case HttpStatusCode.InternalServerError:
                        var errorContent = await apiResponse.Content.ReadAsStringAsync();
                        return new() { IsSuccess = false, Message = errorContent }; // return the error content from the response

                    default:
                        var apiContent = await apiResponse.Content.ReadAsStringAsync(); // retrieve the content from apiResponse
                        
                        var apiResponseDto = JsonConvert.DeserializeObject<ResponseDto>(apiContent); // deserialize the content to ResponseDto
                        return apiResponseDto; // return the deserialized object
                }
            }
            catch (Exception ex)
            {
                var dto = new ResponseDto
                {
                    Message = ex.Message.ToString(),
                    IsSuccess = false
                };
                return dto;

            }

        }

    }
}

// When we receive a response from an API, the data is in JSON format.
// To work with this data in the application, we need to convert it from JSON into a .NET object. This process is called deserialization.
// Deserialization in this context means converting the JSON response from the API into a ResponseDto object so that we can work with the data in the .NET application.

