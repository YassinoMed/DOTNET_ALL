using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductsAPI.Data;
using ProductsAPI.Models;
using Shared.Messages;

namespace ProductsAPI.Controllers;

/// <summary>
/// CRUD des produits et publication de l'événement <see cref="ProductCreated"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductsAPIContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        ProductsAPIContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<ProductsController> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    // GET: api/Products
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
        var products = await _context.Products
            .AsNoTracking()
            .OrderBy(product => product.ProductId)
            .ToListAsync();

        return Ok(products);
    }

    // GET: api/Products/5
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.ProductId == id);

        if (product is null)
        {
            return NotFound(Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Produit introuvable",
                detail: $"Aucun produit ne possède l'identifiant {id}."));
        }

        return Ok(product);
    }

    // POST: api/Products
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Product>> PostProduct(Product product)
    {
        _context.Products.Add(product);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Échec de l'enregistrement du produit {Name}.", product.Name);

            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Enregistrement impossible",
                detail: "Le produit n'a pas pu être enregistré. Aucun événement n'a été publié.");
        }

        // La publication n'a lieu qu'APRÈS une sauvegarde réussie : un produit
        // inexistant en base ne peut jamais être annoncé à OrdersAPI.
        try
        {
            await _publishEndpoint.Publish<ProductCreated>(new ProductCreated
            {
                Id = product.ProductId,
                Name = product.Name
            });
        }
        catch (Exception exception)
        {
            // Pas de pattern Outbox dans ce TP : le produit est persisté mais
            // l'événement n'a pas été publié. On le signale explicitement au
            // client au lieu de prétendre que la communication asynchrone a réussi.
            _logger.LogError(
                exception,
                "Produit {ProductId} enregistré mais publication de ProductCreated impossible.",
                product.ProductId);

            var problem = Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Produit enregistré, événement non publié",
                detail: $"Le produit a été enregistré avec l'identifiant {product.ProductId}, " +
                        "mais l'événement ProductCreated n'a pas pu être publié vers RabbitMQ. " +
                        "La projection locale d'OrdersAPI n'a donc pas été mise à jour.");

            if (problem.Value is ProblemDetails problemDetails)
            {
                problemDetails.Extensions["productId"] = product.ProductId;
            }

            return problem;
        }

        return CreatedAtAction(nameof(GetProduct), new { id = product.ProductId }, product);
    }

    // PUT: api/Products/5
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutProduct(int id, Product product)
    {
        if (id != product.ProductId && product.ProductId != 0)
        {
            return BadRequest(Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Identifiants incohérents",
                detail: "L'identifiant de la route ne correspond pas à celui du corps de la requête."));
        }

        var existing = await _context.Products.FindAsync(id);
        if (existing is null)
        {
            return NotFound(Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Produit introuvable",
                detail: $"Aucun produit ne possède l'identifiant {id}."));
        }

        existing.Name = product.Name;
        existing.StockQte = product.StockQte;
        existing.Price = product.Price;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/Products/5
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound(Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Produit introuvable",
                detail: $"Aucun produit ne possède l'identifiant {id}."));
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
