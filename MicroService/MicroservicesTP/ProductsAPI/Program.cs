using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductsAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// API REST + Swagger
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------------------------------------------------------------------
// EF Core + SQLite : base dédiée Products.db
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La chaîne de connexion 'DefaultConnection' est absente de la configuration.");

var sqliteConnectionString = SqliteConnectionStringResolver.Resolve(
    connectionString,
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<ProductsAPIContext>(options => options.UseSqlite(sqliteConnectionString));

// ---------------------------------------------------------------------------
// MassTransit + RabbitMQ natif (localhost:5672, sans Docker)
// ---------------------------------------------------------------------------
var rabbitMq = builder.Configuration.GetSection("RabbitMq");

builder.Services.AddMassTransit(busRegistration =>
{
    busRegistration.UsingRabbitMq((_, hostRegistration) =>
    {
        hostRegistration.Host(
            rabbitMq["Host"] ?? "localhost",
            rabbitMq.GetValue<ushort?>("Port") ?? 5672,
            rabbitMq["VirtualHost"] ?? "/",
            host =>
            {
                host.Username(rabbitMq["Username"] ?? "guest");
                host.Password(rabbitMq["Password"] ?? "guest");
            });
    });
});

var app = builder.Build();

// Swagger est exposé volontairement dans tous les environnements du TP afin que
// l'interface de test reste disponible quel que soit le mode de lancement.
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
