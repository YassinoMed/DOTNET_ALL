using Microsoft.Data.Sqlite;

namespace ProductsAPI.Data;

/// <summary>
/// Résout le chemin du fichier SQLite par rapport au content root du service.
/// </summary>
/// <remarks>
/// Une chaîne <c>Data Source=Products.db</c> est relative au répertoire courant du
/// processus. Pour que le fichier soit toujours créé dans le répertoire du projet
/// (et non dans un répertoire dépendant du shell), il est résolu explicitement
/// par rapport à <c>IHostEnvironment.ContentRootPath</c>.
/// </remarks>
internal static class SqliteConnectionStringResolver
{
    public static string Resolve(string connectionString, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("La chaîne de connexion SQLite est vide.");
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException("La chaîne de connexion SQLite ne contient pas de Data Source.");
        }

        // Bases en mémoire ou URI SQLite : ne pas transformer.
        if (builder.DataSource == ":memory:" || builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
        }

        return builder.ToString();
    }
}
