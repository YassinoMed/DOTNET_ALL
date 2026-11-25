using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrdersAPI.Data;
using OrdersAPI.Models;

namespace OrdersAPI.Controllers;

/// <summary>
/// CRUD des commandes. OrdersAPI ne consulte jamais ProductsAPI ni Products.db :
/// les informations produit proviennent uniquement de l'événement ProductCreated.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrdersAPIContext _context;

    public OrdersController(OrdersAPIContext context)
    {
        _context = context;
    }

    // GET: api/Orders
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Orders>>> GetOrders()
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .OrderBy(order => order.OrdersId)
            .ToListAsync();

        return Ok(orders);
    }

    // GET: api/Orders/5
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Orders>> GetOrders(int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.OrdersId == id);

        if (order is null)
        {
            return NotFound(Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Commande introuvable",
                detail: $"Aucune commande ne possède l'identifiant {id}."));
        }

        return Ok(order);
    }

    // POST: api/Orders
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Orders>> PostOrders(Orders orders)
    {
        _context.Orders.Add(orders);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrders), new { id = orders.OrdersId }, orders);
    }

    // PUT: api/Orders/5
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutOrders(int id, Orders orders)
    {
        if (id != orders.OrdersId && orders.OrdersId != 0)
        {
            return BadRequest(Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Identifiants incohérents",
                detail: "L'identifiant de la route ne correspond pas à celui du corps de la requête."));
        }

        var existing = await _context.Orders.FindAsync(id);
        if (existing is null)
        {
            return NotFound(Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Commande introuvable",
                detail: $"Aucune commande ne possède l'identifiant {id}."));
        }

        existing.OrderDate = orders.OrderDate;
        existing.CustomerName = orders.CustomerName;
        existing.ProductId = orders.ProductId;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/Orders/5
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrders(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order is null)
        {
            return NotFound(Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Commande introuvable",
                detail: $"Aucune commande ne possède l'identifiant {id}."));
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
