using InfrastructureLayer.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ProductsAPI.Tests;

/// <summary>
/// Fabrique une API isolée par test, avec une base SQLite temporaire.
/// Les variables d'environnement sont utilisées car Program lit la configuration
/// avant la fin de la construction de l'hôte.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"products-api-tests-{Guid.NewGuid():N}.db");

    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("DatabaseProvider", "Sqlite");
        Environment.SetEnvironmentVariable("ConnectionStrings__Sqlite", $"Data Source={_databasePath}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // WebApplicationFactory capture l'hôte à builder.Build() : le bloc de migration
        // placé après Build() dans Program.cs n'est pas exécuté. On applique donc
        // les migrations explicitement sur la base temporaire du test.
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }
}
