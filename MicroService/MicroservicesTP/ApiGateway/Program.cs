using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Ocelot.json est chargé explicitement à la racine de la configuration.
builder.Configuration.AddJsonFile("Ocelot.json", optional: false, reloadOnChange: false);

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

// Ocelot 23 expose une surcharge asynchrone : await app.UseOcelot().
// Le PDF utilise app.UseOcelot().Wait(), API historique à éviter.
await app.UseOcelot();

app.Run();
