using MicroservicesTP.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrdersAPI.Controllers;
using OrdersAPI.Data;
using OrdersAPI.Models;
using Xunit;
using OrderModel = OrdersAPI.Models.Orders;

namespace MicroservicesTP.Tests;

/// <summary>
/// Tests du contrôleur OrdersAPI. Aucune dépendance à ProductsAPI ni à
/// Products.db : la base Orders.db est simulée par du SQLite en mémoire.
/// </summary>
public class OrdersControllerTests : IDisposable
{
    private readonly SqliteInMemoryDatabase _database;
    private readonly OrdersAPIContext _context;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _database = new SqliteInMemoryDatabase();
        _context = new OrdersAPIContext(_database.OptionsFor<OrdersAPIContext>());
        _context.Database.EnsureCreated();
        _controller = new OrdersController(_context);
    }

    [Fact]
    public async Task PostOrders_persiste_et_retourne_201()
    {
        var order = new OrderModel
        {
            OrderDate = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc),
            CustomerName = "Alice",
            ProductId = 1
        };

        var result = await _controller.PostOrders(order);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var saved = Assert.IsType<OrderModel>(created.Value);
        Assert.True(saved.OrdersId > 0);
        Assert.Equal(1, await _context.Orders.CountAsync());
    }

    [Fact]
    public async Task GetOrders_retourne_les_commandes_triees()
    {
        _context.Orders.AddRange(
            new OrderModel { OrderDate = DateTime.UtcNow, CustomerName = "B", ProductId = 2 },
            new OrderModel { OrderDate = DateTime.UtcNow, CustomerName = "A", ProductId = 1 });
        await _context.SaveChangesAsync();

        var result = await _controller.GetOrders();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var orders = Assert.IsAssignableFrom<IEnumerable<OrderModel>>(ok.Value).ToList();
        Assert.Equal(2, orders.Count);
        Assert.True(orders[0].OrdersId < orders[1].OrdersId);
    }

    [Fact]
    public async Task GetOrders_retourne_404_si_absente()
    {
        var result = await _controller.GetOrders(777);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task PutOrders_met_a_jour_et_retourne_204()
    {
        var order = new OrderModel { OrderDate = DateTime.UtcNow, CustomerName = "Avant", ProductId = 1 };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var result = await _controller.PutOrders(order.OrdersId, new OrderModel
        {
            OrdersId = order.OrdersId,
            OrderDate = order.OrderDate,
            CustomerName = "Après",
            ProductId = 3
        });

        Assert.IsType<NoContentResult>(result);

        var reloaded = await _context.Orders.AsNoTracking().SingleAsync(item => item.OrdersId == order.OrdersId);
        Assert.Equal("Après", reloaded.CustomerName);
        Assert.Equal(3, reloaded.ProductId);
    }

    [Fact]
    public async Task DeleteOrders_supprime_et_retourne_204()
    {
        var order = new OrderModel { OrderDate = DateTime.UtcNow, CustomerName = "À supprimer", ProductId = 1 };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var result = await _controller.DeleteOrders(order.OrdersId);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await _context.Orders.CountAsync());
    }

    [Fact]
    public async Task DeleteOrders_retourne_404_si_absente()
    {
        var result = await _controller.DeleteOrders(88888);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }
}
