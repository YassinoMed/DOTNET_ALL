using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductsAPI.Data;
using ProductsAPI.Models;
using Xunit;

namespace MicroservicesTP.Tests;

/// <summary>
/// Démontre la persistance réelle sur fichier : les données restent présentes
/// après fermeture et réouverture du contexte EF Core.
/// Le fichier est créé dans le répertoire temporaire du système, puis supprimé
/// par le test lui-même.
/// </summary>
public class SqlitePersistenceTests
{
    [Fact]
    public async Task Les_donnees_survivent_a_la_reouverture_du_contexte()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"atelier8-persistence-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<ProductsAPIContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using (var firstContext = new ProductsAPIContext(options))
            {
                await firstContext.Database.EnsureCreatedAsync();
                firstContext.Products.Add(new Product { Name = "Persistant", StockQte = 7, Price = 77.7 });
                await firstContext.SaveChangesAsync();
            }

            Assert.True(File.Exists(databasePath), "Le fichier SQLite doit exister sur le disque.");

            await using (var secondContext = new ProductsAPIContext(options))
            {
                var product = await secondContext.Products.AsNoTracking().SingleAsync();

                Assert.Equal("Persistant", product.Name);
                Assert.Equal(7, product.StockQte);
                Assert.Equal(77.7, product.Price, precision: 3);
            }

            // Preuve directe dans le fichier, indépendamment d'EF Core.
            await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Products WHERE Name = 'Persistant';";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());

            Assert.Equal(1, count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
