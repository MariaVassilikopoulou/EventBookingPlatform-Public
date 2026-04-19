using EventBookingPlatform.Interfaces;
using EventBookingPlatform.MappingProfiles;
using EventBookingPlatform.Repositories;
using Microsoft.Azure.Cosmos;
using AutoMapper;
using EventBookingPlatform.Services;
using EventBookingPlatform.Dependencies;
using EventBookingPlatform.AzureServices;
using Azure.Identity;
using EventBookingPlatform.Services.AIServices;
using EventBookingPlatform.Domain.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);
var keyVaultName = "KeyVault-GoEvent";
var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());

// Re-add user secrets after Key Vault so local dev values take precedence over Key Vault
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

string accountEndpoint = builder.Configuration["CosmosDb-AccountEndpoint"];
string accountKey = builder.Configuration["CosmosDb-AccountKey"];
string databaseName = builder.Configuration["CosmosDb-DatabaseName"];

var cosmosDbSettings = new CosmosDbSettings
{
    AccountEndpoint = accountEndpoint,
    AccountKey = accountKey,
    DatabaseName = databaseName
};

var cosmosClient = new CosmosClient(accountEndpoint, accountKey);
var databaseResponse = cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName).GetAwaiter().GetResult();
Console.WriteLine($"Connected to database: {databaseResponse.Database.Id}");
var database = databaseResponse.Database;

database.CreateContainerIfNotExistsAsync("Events", "/partitionKey").GetAwaiter().GetResult();
database.CreateContainerIfNotExistsAsync("Bookings", "/partitionKey").GetAwaiter().GetResult();
Console.WriteLine($"Cosmos setup complete. Database={databaseName}");





builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(cosmosDbSettings));
//builder.Services.Configure<CosmosDbSettings>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.AddSingleton(cosmosClient);
builder.Services.AddSingleton<ServiceBusService>();
// Register repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IBookingService, BookingService>();
//builder.Services.AddScoped<IGenericRepository<Event>, GenericRepository<Event>>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAutoMapper(typeof(MappingProfiles));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGenWithAuth();
builder.Services.AddScoped<ITokenProvider, TokenProvider>();
builder.Services.AddScoped<AIAssistantService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins(
                "http://localhost:3000",
                "https://event-booking-frontend-public.vercel.app",
                "https://flowvent.online",
                "https://www.flowvent.online")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddIdentityAndJwtAuth(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow = 2;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    options.RejectionStatusCode = 429;
});

builder.Services.AddProblemDetails();


var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EventBookingPlatform API V1");
        c.RoutePrefix = string.Empty;
    });
}
else
{

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EventBookingPlatform API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowFrontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
