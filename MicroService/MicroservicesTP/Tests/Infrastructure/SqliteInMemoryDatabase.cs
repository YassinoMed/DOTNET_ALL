using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MicroservicesTP.Tests.Infrastructure;

/// <summary>
/// Base SQLite réelle en mémoire, partagée par un contexte EF Core pendant la
/// durée d'un test. Aucune base de production n'est touchée.
/// </summary>
internal sealed class SqliteInMemoryDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteInMemoryDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public DbContextOptions<TContext> OptionsFor<TContext>()
        where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
