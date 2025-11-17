using AutoMapper;
using E_commerce.Services.ShoppingCartAPI;
using E_commerce.Services.ShoppingCartAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using E_commerce.Services.ShoppingCartAPI.Extensions;
using E_commerce.Services.ShoppingCartAPI.Service.IService;
using E_commerce.Services.ShoppingCartAPI.Service;
using E_commerce.Services.ShoppingCartAPI.Utility;
using Ecommerce.MessageBus;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// create a mapper
IMapper mapper = MappingConfig.RegisterMaps().CreateMapper();
// add the mapper to the service
builder.Services.AddSingleton(mapper);
// we want to use AutoMapper using dependency injection
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Registering the ProductService as a scoped service.
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICouponService, CouponService>(); // inject the CouponService

builder.Services.AddHttpContextAccessor(); // add IHttpContextAccessor to the service container
builder.Services.AddScoped<BackendAPIAuthenticationHttpClientHandler>();
builder.Services.AddScoped<IMessageBus, MessageBus>(); // Registering the MessageBus service for publishing messages to the message bus

// Registering an HttpClient named "Product" in the dependency injection container.
// The BaseAddress (root URL) for this client is configured from appsettings.json under ServiceUrls:ProductAPI.
// This allows making HTTP requests to the ProductAPI without hardcoding the base URL in the code.
builder.Services.AddHttpClient("Product", u => u.BaseAddress = new Uri(builder.Configuration["ServiceUrls:ProductAPI"])).AddHttpMessageHandler<BackendAPIAuthenticationHttpClientHandler>();
// Register HttpClient with the base address for Coupon API
builder.Services.AddHttpClient("Coupon", u => u.BaseAddress = new Uri(builder.Configuration["ServiceUrls:CouponAPI"])).AddHttpMessageHandler<BackendAPIAuthenticationHttpClientHandler>();


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.AddSecurityDefinition(name: JwtBearerDefaults.AuthenticationScheme, securityScheme: new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter the JWT token with 'Bearer ' prefix: `Bearer Generated-JWT-Token`",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference= new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id=JwtBearerDefaults.AuthenticationScheme
                }
            }, new string[] { }
        }
    });
});

builder.AddAppAuthentication();

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Health check endpoint for Azure Container Apps
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ShoppingCartAPI", timestamp = DateTime.UtcNow }));

if (!app.Environment.IsProduction())
{
    ApplyMigration();
}

app.Run();

void ApplyMigration()
{
    // I want to get the ApplicationDbContext service here and check if there are any pending migration.
    // If there are any pending migration, I want to apply them.

    // Get all the services from the service container
    using (var scope = app.Services.CreateScope())
    {
        var _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (_db.Database.GetPendingMigrations().Count() > 0)
        {
            _db.Database.Migrate();
        }
    }
}
// SQL Server Management Studio (SSMS)
