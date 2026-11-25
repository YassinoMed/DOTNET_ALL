using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SecureAPI_JWT.Tests;

/// <summary>
/// Factory de test qui isole une base SQLite temporaire par instance.
/// L'environnement est <c>Testing</c> : pas de redirection HTTPS, Swagger actif,
/// aucun seed administrateur implicite. La base de développement n'est jamais utilisée.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Clé JWT réservée aux tests (jamais utilisée par l'application de développement).</summary>
    public const string TestJwtKey =
        "test-only-jwt-signing-key-2026-secure-api-tp9-not-a-production-secret";

    private readonly string _dbPath;
    private readonly IReadOnlyDictionary<string, string?> _settings;
    private readonly bool _deleteDatabaseOnDispose;

    public TestWebApplicationFactory(
        IDictionary<string, string?>? settings = null,
        string? databasePath = null,
        bool deleteDatabaseOnDispose = true)
    {
        _dbPath = databasePath ??
                  Path.Combine(Path.GetTempPath(), $"secureapi_test_{Guid.NewGuid():N}.db");
        _deleteDatabaseOnDispose = deleteDatabaseOnDispose;

        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            // Aucun administrateur implicite dans les tests : chaque test le crée s'il en a besoin.
            ["AdminUser:Username"] = null,
            ["AdminUser:Email"] = null,
            ["AdminUser:Password"] = null,
            ["JwtSettings:Key"] = TestJwtKey,
            ["JwtSettings:Issuer"] = "SecureAPI_JWT_Tests",
            ["JwtSettings:Audience"] = "SecureAPI_JWT_Tests_Client",
            ["JwtSettings:DurationInDays"] = "1",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
        };

        if (settings is not null)
        {
            foreach (var (key, value) in settings)
            {
                merged[key] = value;
            }
        }

        // Garantit l'isolation : la connexion de test ne peut pas être remplacée par un appelant.
        merged["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath};Pooling=False";

        _settings = merged;
    }

    public string DatabasePath => _dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(_settings));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_deleteDatabaseOnDispose)
        {
            DeleteDatabaseFiles(_dbPath);
        }
    }

    /// <summary>Supprime le fichier SQLite et ses fichiers annexes (WAL/SHM).</summary>
    public static void DeleteDatabaseFiles(string databasePath)
    {
        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            try
            {
                var path = databasePath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Un handle SQLite peut encore être ouvert : nettoyage non bloquant.
            }
            catch (UnauthorizedAccessException)
            {
                // Idem : ne fait jamais échouer un test pour un fichier temporaire.
            }
        }
    }
}
