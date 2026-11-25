using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrdersAPI.Consumer;
using OrdersAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// API REST + Swagger
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------------------------------------------------------------------
// EF Core + SQLite : base dédiée Orders.db (jamais Products.db)
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La chaîne de connexion 'DefaultConnection' est absente de la configuration.");

var sqliteConnectionString = SqliteConnectionStringResolver.Resolve(
    connectionString,
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<OrdersAPIContext>(options => options.UseSqlite(sqliteConnectionString));

// ---------------------------------------------------------------------------
// MassTransit + RabbitMQ natif : consommation de ProductCreated
// ---------------------------------------------------------------------------
var rabbitMq = builder.Configuration.GetSection("RabbitMq");

builder.Services.AddMassTransit(busRegistration =>
{
    busRegistration.AddConsumer<ProductCreatedConsumer>();

    busRegistration.UsingRabbitMq((context, hostRegistration) =>
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

        // File d'attente imposée par le PDF (page 49). MassTransit la lie à
        // l'exchange fanout du contrat ProductCreated.
        hostRegistration.ReceiveEndpoint("event-listener", endpoint =>
        {
            endpoint.ConfigureConsumer<ProductCreatedConsumer>(context);
        });
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
