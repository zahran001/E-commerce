using E_commerce.Services.AuthAPI.Data;
using E_commerce.Services.AuthAPI.Models;
using E_commerce.Services.AuthAPI.Service;
using E_commerce.Services.AuthAPI.Service.IService;
using Ecommerce.Shared.Middleware;
using Ecommerce.MessageBus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Service", "AuthAPI")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .CreateLogger();

builder.Host.UseSerilog();

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
builder.Services.AddHttpContextAccessor();
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

app.UseCorrelationId();

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
        Log.Warning(ex, "Health check failed - database connectivity issue");
        return Results.Json(
            new { status = "unhealthy", service = "AuthAPI", timestamp = DateTime.UtcNow, error = ex.Message },
            statusCode: 503);
    }
});

if (!app.Environment.IsProduction())
{
	ApplyMigration();
}

try
{
	Log.Information("Starting AuthAPI service");
	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "AuthAPI terminated unexpectedly");
	throw;
}
finally
{
	Log.CloseAndFlush();
}

void ApplyMigration()
{
	// I want to get the ApplicationDbContext service here and check if there are any pending migration.
	// If there are any pending migration, I want to apply them.

	// Get all the services from the service container
	using (var scope = app.Services.CreateScope())
	{
		try
		{
			var _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var pendingMigrations = _db.Database.GetPendingMigrations().ToList();

			if (pendingMigrations.Count > 0)
			{
				Log.Information("Applying {MigrationCount} pending database migrations", pendingMigrations.Count);
				_db.Database.Migrate();
				Log.Information("Successfully applied all pending database migrations");
			}
			else
			{
				Log.Information("No pending database migrations to apply");
			}
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Critical error applying database migrations - service startup failed");
			throw;
		}
	}
}

// We will be using .NET Identity is a package that will create all the identity related tables automatically with EF Core.