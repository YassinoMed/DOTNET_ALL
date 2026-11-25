using MassTransit;
using MicroservicesTP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrdersAPI.Consumer;
using OrdersAPI.Data;
using Shared.Messages;
using Xunit;

namespace MicroservicesTP.Tests;

/// <summary>
/// Tests du consumer ProductCreatedConsumer, en particulier l'idempotence :
/// deux livraisons du même événement ne doivent jamais créer deux projections.
/// </summary>
public class ProductCreatedConsumerTests : IDisposable
{
    private readonly SqliteInMemoryDatabase _database;
    private readonly OrdersAPIContext _context;
    private readonly ProductCreatedConsumer _consumer;

    public ProductCreatedConsumerTests()
    {
        _database = new SqliteInMemoryDatabase();
        _context = new OrdersAPIContext(_database.OptionsFor<OrdersAPIContext>());
        _context.Database.EnsureCreated();
        _consumer = new ProductCreatedConsumer(_context, NullLogger<ProductCreatedConsumer>.Instance);
    }

    [Fact]
    public async Task Consume_cree_la_projection_locale()
    {
        await ConsumeAsync(new ProductCreated { Id = 42, Name = "Produit 42" });

        var projection = Assert.Single(await _context.Products.ToListAsync());
        Assert.Equal(42, projection.Id);
        Assert.Equal("Produit 42", projection.Name);
    }

    [Fact]
    public async Task Consume_deux_fois_le_meme_evenement_ne_cree_pas_de_doublon()
    {
        var message = new ProductCreated { Id = 42, Name = "Produit 42" };

        await ConsumeAsync(message);
        await ConsumeAsync(message);

        Assert.Equal(1, await _context.Products.CountAsync(product => product.Id == 42));
    }

    [Fact]
    public async Task Consume_un_evenement_avec_un_nouveau_nom_met_a_jour_sans_dupliquer()
    {
        await ConsumeAsync(new ProductCreated { Id = 42, Name = "Ancien nom" });
        await ConsumeAsync(new ProductCreated { Id = 42, Name = "Nouveau nom" });

        var projection = Assert.Single(await _context.Products.ToListAsync());
        Assert.Equal(42, projection.Id);
        Assert.Equal("Nouveau nom", projection.Name);
    }

    [Fact]
    public async Task Consume_deux_produits_differents_cree_deux_lignes()
    {
        await ConsumeAsync(new ProductCreated { Id = 1, Name = "Un" });
        await ConsumeAsync(new ProductCreated { Id = 2, Name = "Deux" });

        Assert.Equal(2, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task Consume_refuse_un_identifiant_invalide()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ConsumeAsync(new ProductCreated { Id = 0, Name = "Invalide" }));

        Assert.Equal(0, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task Consume_refuse_un_nom_vide()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ConsumeAsync(new ProductCreated { Id = 5, Name = "   " }));

        Assert.Equal(0, await _context.Products.CountAsync());
    }

    private async Task ConsumeAsync(ProductCreated message)
    {
        var consumeContext = new Mock<ConsumeContext<ProductCreated>>();
        consumeContext.SetupGet(context => context.Message).Returns(message);
        consumeContext.SetupGet(context => context.CancellationToken).Returns(CancellationToken.None);

        await _consumer.Consume(consumeContext.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }
}
