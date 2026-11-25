using MassTransit;
using MicroservicesTP.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProductsAPI.Controllers;
using ProductsAPI.Data;
using ProductsAPI.Models;
using Shared.Messages;
using Xunit;

namespace MicroservicesTP.Tests;

/// <summary>
/// Tests du contrôleur ProductsAPI (CRUD + publication de ProductCreated).
/// Aucun broker n'est requis : IPublishEndpoint est mocké.
/// </summary>
public class ProductsControllerTests : IDisposable
{
    private readonly SqliteInMemoryDatabase _database;
    private readonly Mock<IPublishEndpoint> _publishEndpoint;
    private readonly ProductsAPIContext _context;
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        _database = new SqliteInMemoryDatabase();
        _publishEndpoint = new Mock<IPublishEndpoint>(MockBehavior.Loose);

        _context = new ProductsAPIContext(_database.OptionsFor<ProductsAPIContext>());
        _context.Database.EnsureCreated();

        _controller = new ProductsController(
            _context,
            _publishEndpoint.Object,
            NullLogger<ProductsController>.Instance);
    }

    [Fact]
    public async Task PostProduct_Persiste_et_publie_apres_la_sauvegarde()
    {
        var product = new Product { Name = "Clavier", StockQte = 10, Price = 49.90 };

        var result = await _controller.PostProduct(product);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var saved = Assert.IsType<Product>(created.Value);

        Assert.True(saved.ProductId > 0, "L'identifiant doit être généré par SQLite.");
        Assert.Equal(1, await _context.Products.CountAsync());

        // L'identifiant publié est celui généré par la base : la publication a donc
        // bien eu lieu APRÈS SaveChangesAsync (sinon Id vaudrait 0).
        _publishEndpoint.Verify(
            endpoint => endpoint.Publish<ProductCreated>(
                It.Is<ProductCreated>(message => message.Id == saved.ProductId && message.Name == "Clavier"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PostProduct_si_la_publication_echoue_retourne_503_et_le_produit_reste_persiste()
    {
        _publishEndpoint
            .Setup(endpoint => endpoint.Publish<ProductCreated>(
                It.IsAny<ProductCreated>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Broker RabbitMQ indisponible."));

        var result = await _controller.PostProduct(new Product { Name = "Souris", StockQte = 2, Price = 12.5 });

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);

        // Le produit existe réellement en base : l'API ne prétend pas le contraire.
        Assert.Equal(1, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task GetProducts_retourne_tous_les_produits()
    {
        _context.Products.AddRange(
            new Product { Name = "A", StockQte = 1, Price = 1 },
            new Product { Name = "B", StockQte = 2, Price = 2 });
        await _context.SaveChangesAsync();

        var result = await _controller.GetProducts();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var products = Assert.IsAssignableFrom<IEnumerable<Product>>(ok.Value).ToList();

        Assert.Equal(2, products.Count);
        Assert.Equal(new[] { "A", "B" }, products.Select(product => product.Name));
    }

    [Fact]
    public async Task GetProduct_retourne_404_si_absent()
    {
        var result = await _controller.GetProduct(4242);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task PutProduct_met_a_jour_et_retourne_204()
    {
        var product = new Product { Name = "Ancien", StockQte = 1, Price = 1 };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var result = await _controller.PutProduct(product.ProductId, new Product
        {
            ProductId = product.ProductId,
            Name = "Nouveau",
            StockQte = 5,
            Price = 9.99
        });

        Assert.IsType<NoContentResult>(result);

        var reloaded = await _context.Products.AsNoTracking().SingleAsync(item => item.ProductId == product.ProductId);
        Assert.Equal("Nouveau", reloaded.Name);
        Assert.Equal(5, reloaded.StockQte);
        Assert.Equal(9.99, reloaded.Price);
    }

    [Fact]
    public async Task PutProduct_retourne_404_si_absent()
    {
        var result = await _controller.PutProduct(999, new Product { ProductId = 999, Name = "X", StockQte = 1, Price = 1 });

        var problem = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_supprime_et_retourne_204()
    {
        var product = new Product { Name = "À supprimer", StockQte = 1, Price = 1 };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var result = await _controller.DeleteProduct(product.ProductId);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task DeleteProduct_retourne_404_si_absent()
    {
        var result = await _controller.DeleteProduct(123456);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }
}
