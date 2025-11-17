using BizFirstMetaMessenger.Data;
using BizFirstMetaMessenger.Models;
using BizFirstMetaMessenger.Services;
using Microsoft.EntityFrameworkCore;

// Create the web application builder
var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure Meta settings
builder.Services.Configure<MetaConfiguration>(options =>
{
    var metaSection = builder.Configuration.GetSection("Meta");
    options.VerifyToken = metaSection["VerifyToken"] ?? Environment.GetEnvironmentVariable("META_VERIFY_TOKEN");
    options.AppSecret = metaSection["AppSecret"] ?? Environment.GetEnvironmentVariable("META_APP_SECRET");
    options.PageAccessToken = metaSection["PageAccessToken"];
});

// Add services to the container
builder.Services.AddControllers();

// Configure SQLite database
builder.Services.AddDbContext<WebhookDbContext>(options =>
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, "webhooks.db");
    options.UseSqlite($"Data Source={dbPath}");
});

// Register application services (dependency injection)
builder.Services.AddScoped<ISignatureValidator, SignatureValidator>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddSingleton<IWebhookStorageService, WebhookStorageService>();
builder.Services.AddScoped<IWebhookDatabaseService, WebhookDatabaseService>();

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Meta Messenger Webhook API",
        Version = "v1",
        Description = "API for receiving and processing Meta Messenger webhook events",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "BizFirst AI",
            Url = new Uri("https://github.com/yourusername/BizFirstMetaMessenger")
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Build the application
var app = builder.Build();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
    dbContext.Database.EnsureCreated();
    app.Logger.LogInformation("✓ Database initialized");
}

// Enable request buffering for reading body multiple times
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next();
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    // Enable Swagger in development mode
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Meta Messenger Webhook API v1");
        options.RoutePrefix = "swagger"; // Access Swagger at /swagger
    });
}

// Enable HTTPS redirection (optional - uncomment if needed)
// app.UseHttpsRedirection();

// Map controllers
app.MapControllers();

// Log startup information
var logger = app.Logger;
logger.LogInformation("=== Meta Messenger Webhook Server Started ===");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("Swagger UI available at: /swagger");

// Start the application
app.Run();
