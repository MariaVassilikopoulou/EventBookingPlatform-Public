using EventBookingPlatform.Interfaces;
using EventBookingPlatform.MappingProfiles;
using EventBookingPlatform.Repositories;
using Microsoft.Azure.Cosmos;
using AutoMapper;
using EventBookingPlatform.Services;
using EventBookingPlatform.Dependencies;


var builder = WebApplication.CreateBuilder(args);


string accountEndpoint = builder.Configuration["CosmosDb:AccountEndpoint"];
string accountKey = builder.Configuration["CosmosDb:AccountKey"];
string databaseName = builder.Configuration["CosmosDb:DatabaseName"];


var cosmosClient = new CosmosClient(accountEndpoint, accountKey);
var databaseResponse = cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName).GetAwaiter().GetResult();
Console.WriteLine($"Connected to database: {databaseResponse.Database.Id}");
var database = databaseResponse.Database;

database.CreateContainerIfNotExistsAsync("Events", "/partitionKey").GetAwaiter().GetResult();
database.CreateContainerIfNotExistsAsync("Bookings", "/partitionKey").GetAwaiter().GetResult();
Console.WriteLine($"Cosmos setup complete. Database={databaseName}");

builder.Services.Configure<CosmosDbSettings>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.AddSingleton(cosmosClient);

// Register repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAutoMapper(typeof(MappingProfiles));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGenWithAuth();
builder.Services.AddScoped<ITokenProvider, TokenProvider>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddIdentityAndJwtAuth(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
