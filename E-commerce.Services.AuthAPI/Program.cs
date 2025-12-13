using E_commerce.Services.AuthAPI.Data;
using E_commerce.Services.AuthAPI.Models;
using E_commerce.Services.AuthAPI.Service;
using E_commerce.Services.AuthAPI.Service.IService;
using Ecommerce.MessageBus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
	option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("ApiSettings:JwtOptions"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>()
	.AddDefaultTokenProviders();
// Bridge between EF Core and .NET Identity

builder.Services.AddControllers();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IMessageBus, MessageBus>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAll", policy =>
	{
		policy.AllowAnyOrigin()
			  .AllowAnyMethod()
			  .AllowAnyHeader();
	});
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

app.UseAuthentication();
// AuthAPI is responsible for authentication and authorization.
// Authentication must always come before Authorization in the pipeline.

app.UseAuthorization();

app.MapControllers();

// Health check endpoint for Azure Container Apps - database aware to trigger DB wake-up
app.MapGet("/health", async (ApplicationDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();

        if (!canConnect)
        {
            return Results.Json(
                new { status = "unhealthy", service = "AuthAPI", timestamp = DateTime.UtcNow, database = "disconnected" },
                statusCode: 503);
        }

        return Results.Ok(new {
            status = "healthy",
            service = "AuthAPI",
            timestamp = DateTime.UtcNow,
            database = "connected"
        });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "AuthAPI", timestamp = DateTime.UtcNow, error = ex.Message },
            statusCode: 503);
    }
});

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

// We will be using .NET Identity is a package that will create all the identity related tables automatically with EF Core.