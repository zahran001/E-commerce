using E_commerce.Web.Service;
using E_commerce.Web.Service.IService;
using E_commerce.Web.Utility;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.ExpireTimeSpan = TimeSpan.FromHours(10);
		options.LoginPath = "/Auth/Login";  // Set the login path
		options.AccessDeniedPath = "/Auth/AccessDenied"; // Set the logout path
	});

// Add services to the container.
builder.Services.AddControllersWithViews();
// Configure the HTTP client
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IProductService, ProductService>();
builder.Services.AddHttpClient<ICouponService, CouponService>();
builder.Services.AddHttpClient<IAuthService, AuthService>();
builder.Services.AddHttpClient<ICartService, CartService>();

// register services with a scoped lifetime -  a new instance of the service will be created for each HTTP request
builder.Services.AddScoped<IBaseService, BaseService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenProvider, TokenProvider>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();

// Register the CouponService
StaticDetails.CouponApiBase = builder.Configuration["ServiceUrls:CouponAPI"];
// Register the AuthService
StaticDetails.AuthApiBase = builder.Configuration["ServiceUrls:AuthAPI"];
// Register the ProductService
StaticDetails.ProductApiBase = builder.Configuration["ServiceUrls:ProductAPI"];
// Register the ShoppingCartService
StaticDetails.ShoppingCartAPIBase = builder.Configuration["ServiceUrls:ShoppingCartAPI"];

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value are 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();  // Ensure authentication middleware is used
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Health check endpoint for Azure Container Apps
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "WebMVC", timestamp = DateTime.UtcNow }));

app.Run();


/*
Scoped services are registered using AddScoped<TInterface, TImplementation>(), 
meaning a new instance is created per HTTP request and reused within that request.

Scoped lifecycle:
	A new instance is created for each HTTP request.
	The instance persists throughout the request and is shared within that request.
	The same instance is used if the service is injected multiple times within the same request.
	It is disposed of when the request ends.
 */