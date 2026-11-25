using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrdersAPI.Data;
using OrdersAPI.Models;
using Shared.Messages;

namespace OrdersAPI.Consumer;

/// <summary>
/// Consomme <see cref="ProductCreated"/> et maintient la projection locale
/// <c>Products(Id, Name)</c> dans Orders.db.
/// </summary>
/// <remarks>
/// Idempotence : la clé primaire de la projection est l'identifiant métier du
/// produit (<see cref="ProductCreated.Id"/>). La consommation vérifie d'abord
/// l'existence de la ligne, puis insère ou met à jour le nom. Deux livraisons du
/// même événement ne créent donc jamais deux lignes. La contrainte PRIMARY KEY
/// reste la protection ultime en cas de consommation concurrente.
/// </remarks>
public class ProductCreatedConsumer : IConsumer<ProductCreated>
{
    private readonly OrdersAPIContext _context;
    private readonly ILogger<ProductCreatedConsumer> _logger;

    public ProductCreatedConsumer(OrdersAPIContext context, ILogger<ProductCreatedConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProductCreated> context)
    {
        var message = context.Message;

        if (message.Id <= 0)
        {
            // Message métier invalide : on ne pollue pas la projection. MassTransit
            // place le message en échec dans la file event-listener_error.
            throw new InvalidOperationException(
                $"ProductCreated invalide : identifiant {message.Id} non strictement positif.");
        }

        if (string.IsNullOrWhiteSpace(message.Name))
        {
            throw new InvalidOperationException(
                $"ProductCreated invalide : le nom du produit {message.Id} est vide.");
        }

        if (message.Name.Length > 100)
        {
            throw new InvalidOperationException(
                $"ProductCreated invalide : le nom du produit {message.Id} dépasse 100 caractères.");
        }

        var existing = await _context.Products
            .FirstOrDefaultAsync(product => product.Id == message.Id, context.CancellationToken);

        if (existing is null)
        {
            var projection = new Product
            {
                Id = message.Id,
                Name = message.Name
            };

            _context.Products.Add(projection);

            try
            {
                await _context.SaveChangesAsync(context.CancellationToken);
                _logger.LogInformation(
                    "Projection produit créée : Id={ProductId}, Name={ProductName}.",
                    projection.Id,
                    projection.Name);
            }
            catch (DbUpdateException exception)
            {
                // Course entre deux livraisons du même message : la clé primaire a
                // rejeté la seconde insertion. On repart de l'état réel en base.
                _context.Entry(projection).State = EntityState.Detached;

                var concurrent = await _context.Products
                    .FirstOrDefaultAsync(product => product.Id == message.Id, context.CancellationToken);

                if (concurrent is null)
                {
                    throw;
                }

                if (concurrent.Name != message.Name)
                {
                    concurrent.Name = message.Name;
                    await _context.SaveChangesAsync(context.CancellationToken);
                }

                _logger.LogWarning(
                    exception,
                    "Livraison concurrente détectée pour le produit {ProductId} : aucun doublon créé.",
                    message.Id);
            }

            return;
        }

        if (existing.Name != message.Name)
        {
            existing.Name = message.Name;
            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "Projection produit mise à jour : Id={ProductId}, Name={ProductName}.",
                existing.Id,
                existing.Name);
        }
        else
        {
            _logger.LogInformation(
                "ProductCreated déjà appliqué pour le produit {ProductId} : aucun doublon.",
                message.Id);
        }
    }
}
