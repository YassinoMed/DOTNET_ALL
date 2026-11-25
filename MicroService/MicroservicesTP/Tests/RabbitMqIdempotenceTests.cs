using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shared.Messages;
using Xunit;

namespace MicroservicesTP.Tests;

/// <summary>
/// Test d'idempotence sur un VRAI broker RabbitMQ.
/// </summary>
/// <remarks>
/// Ce test n'utilise aucun mock : il démarre un bus MassTransit, publie deux fois
/// le même <see cref="ProductCreated"/> vers RabbitMQ, puis vérifie la projection
/// réellement écrite dans Orders.db par OrdersAPI (processus séparé).
///
/// Il dépend donc de :
///   - RabbitMQ natif actif sur localhost:5672 ;
///   - OrdersAPI démarré et abonné à la file event-listener ;
///   - la variable RABBITMQ_E2E=1 fournie par scripts/e2e.sh.
///
/// Sans ces préconditions, le test est marqué SKIPPED (jamais PASS silencieux).
/// </remarks>
[Trait("Category", "RabbitMq")]
public class RabbitMqIdempotenceTests
{
    [SkippableFact]
    public async Task Rejouer_le_meme_evenement_ne_cree_pas_de_doublon_dans_Orders_db()
    {
        Skip.IfNot(
            Environment.GetEnvironmentVariable("RABBITMQ_E2E") == "1",
            "Test broker réel : exécuter scripts/e2e.sh (RabbitMQ + OrdersAPI requis).");

        var ordersDbPath = Environment.GetEnvironmentVariable("ORDERS_DB_PATH");
        Skip.If(
            string.IsNullOrWhiteSpace(ordersDbPath) || !File.Exists(ordersDbPath),
            $"ORDERS_DB_PATH invalide ou Orders.db introuvable ({ordersDbPath}).");

        var productIdRaw = Environment.GetEnvironmentVariable("IDEMPOTENCE_PRODUCT_ID");
        Skip.If(
            !int.TryParse(productIdRaw, out var productId) || productId <= 0,
            "IDEMPOTENCE_PRODUCT_ID absent ou invalide.");

        var originalName = Environment.GetEnvironmentVariable("IDEMPOTENCE_PRODUCT_NAME");
        Skip.If(string.IsNullOrWhiteSpace(originalName), "IDEMPOTENCE_PRODUCT_NAME absent.");

        var updatedName = $"{originalName}-updated";

        // La projection initiale a été créée par le POST HTTP réel du script E2E.
        var initial = await ReadProjectionAsync(ordersDbPath!, productId);
        Assert.Equal(1, initial.Count);
        Assert.Equal(originalName, initial.Name);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMassTransit(busRegistration =>
        {
            busRegistration.UsingRabbitMq((_, hostRegistration) =>
            {
                hostRegistration.Host("localhost", 5672, "/", host =>
                {
                    host.Username("guest");
                    host.Password("guest");
                });
            });
        });

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IBusControl>();

        await bus.StartAsync();

        try
        {
            // Premier rejeu avec un nouveau nom : doit mettre à jour la projection.
            await bus.Publish(new ProductCreated { Id = productId, Name = updatedName });

            var deadline = DateTime.UtcNow.AddSeconds(20);
            string? currentName = initial.Name;
            while (DateTime.UtcNow < deadline)
            {
                currentName = (await ReadProjectionAsync(ordersDbPath!, productId)).Name;
                if (currentName == updatedName)
                {
                    break;
                }

                await Task.Delay(250);
            }

            Assert.Equal(updatedName, currentName);

            // Second rejeu strictement identique : doit être absorbé sans doublon.
            await bus.Publish(new ProductCreated { Id = productId, Name = updatedName });
            await Task.Delay(2500);
        }
        finally
        {
            await bus.StopAsync(TimeSpan.FromSeconds(10));
        }

        var final = await ReadProjectionAsync(ordersDbPath!, productId);
        Assert.Equal(1, final.Count);
        Assert.Equal(updatedName, final.Name);
    }

    private static async Task<(int Count, string? Name)> ReadProjectionAsync(string databasePath, int productId)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var connection = new SqliteConnection(
                    $"Data Source={databasePath};Mode=ReadOnly;Default Timeout=5");
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*), MAX(Name) FROM Products WHERE Id = $id;";
                command.Parameters.AddWithValue("$id", productId);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1));
                }

                return (0, null);
            }
            catch (SqliteException) when (attempt < 20)
            {
                // OrdersAPI peut détenir brièvement un verrou d'écriture.
                await Task.Delay(150);
            }
        }
    }
}
