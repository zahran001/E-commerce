using E_commerce.Web.Models;

namespace E_commerce.Web.Service.IService
{
    public interface IBaseService
    {
        Task<ResponseDto?> SendAsync(RequestDto requestDto, bool withBearer = true);
    }
}

// From the Web project, we will be calling the APIs.
// A particular service is responsible for calling the endpoints. So we need to create a service class in the Web project.

// SendAsync:A method that takes a RequestDto object as input and returns a Task<ResponseDto?>.
// Parameters:
// RequestDto requestDto: Represents the data being sent with the request.
// Return Type:
// Task<ResponseDto?>: The method is asynchronous (denoted by Task), meaning it can run without blocking the main thread.
// ResponseDto ?: The method may return a ResponseDto or it can return null (denoted by the ?).
