using E_commerce.Services.EmailAPI.Data;
using Ecommerce.Services.EmailAPI.Extension;
using Ecommerce.Services.EmailAPI.Messaging;
using Ecommerce.Services.EmailAPI.Services;
using Microsoft.Azure.Amqp;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var optionBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionBuilder.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddSingleton(new EmailService(optionBuilder.Options)); // singleton implementation of the EmailService
// AzureServiceBusConsumer is a singleton and the DbContext is a scoped service.
// We cannot consume a scoped service into a singleton service.
// Thus get a new ApplicationDbContext implementation that is singleton.

builder.Services.AddSingleton<IAzureServiceBusConsumer, AzureServiceBusConsumer>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Health check endpoint for Azure Container Apps
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "EmailAPI", timestamp = DateTime.UtcNow }));

if (!app.Environment.IsProduction())
{
    ApplyMigration();
}

app.UseAzureServiceBusConsumer(); // extension method

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
