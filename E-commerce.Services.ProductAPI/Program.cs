using AutoMapper;
using E_commerce.Services.ProductAPI;
using E_commerce.Services.ProductAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.OpenApi.Models;
using E_commerce.Services.ProductAPI.Extensions;
using Serilog;
using Ecommerce.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Service", "ProductAPI")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .CreateLogger();

builder.Host.UseSerilog();

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

builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAll", policy =>
	{
		policy.AllowAnyOrigin()
			  .AllowAnyMethod()
			  .AllowAnyHeader();
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

if (app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

app.UseCorrelationId();

app.UseAuthentication();

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
                new { status = "unhealthy", service = "ProductAPI", timestamp = DateTime.UtcNow, database = "disconnected" },
                statusCode: 503);
        }

        return Results.Ok(new {
            status = "healthy",
            service = "ProductAPI",
            timestamp = DateTime.UtcNow,
            database = "connected"
        });
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Health check failed - database connectivity issue");
        return Results.Json(
            new { status = "unhealthy", service = "ProductAPI", timestamp = DateTime.UtcNow, error = ex.Message },
            statusCode: 503);
    }
});

if (!app.Environment.IsProduction())
{
	ApplyMigration();
}

try
{
	Log.Information("Starting ProductAPI service");
	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "ProductAPI terminated unexpectedly");
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
// SQL Server Management Studio (SSMS)
