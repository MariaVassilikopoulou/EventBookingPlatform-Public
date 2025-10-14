using EventBookingPlatform.Interfaces;
using EventBookingPlatform.MappingProfiles;
using EventBookingPlatform.Repositories;
using Microsoft.Azure.Cosmos;
using AutoMapper;


var builder = WebApplication.CreateBuilder(args);

// Load config
string accountEndpoint = builder.Configuration["CosmosDb:AccountEndpoint"];
string accountKey = builder.Configuration["CosmosDb:AccountKey"];
string databaseName = builder.Configuration["CosmosDb:DatabaseName"];

// Create Cosmos client (outside DI)
var cosmosClient = new CosmosClient(accountEndpoint, accountKey);

// Ensure database and containers exist
var databaseResponse = cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName).GetAwaiter().GetResult();
Console.WriteLine($"Connected to database: {databaseResponse.Database.Id}");

var database = databaseResponse.Database;

database.CreateContainerIfNotExistsAsync("Events", "/partitionKey").GetAwaiter().GetResult();
database.CreateContainerIfNotExistsAsync("Bookings", "/partitionKey").GetAwaiter().GetResult();

Console.WriteLine($"Cosmos setup complete. Database={databaseName}");

builder.Services.Configure<CosmosDbSettings>(
    builder.Configuration.GetSection("CosmosDb"));


// Register Cosmos client in DI as singleton
builder.Services.AddSingleton(cosmosClient);

// Register repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAutoMapper(typeof(MappingProfiles));
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

app.Run();
