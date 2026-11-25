using Microsoft.EntityFrameworkCore;
using OrdersAPI.Models;

namespace OrdersAPI.Data;

/// <summary>
/// Contexte EF Core d'OrdersAPI. Il ne connaît que la base Orders.db
/// (tables <c>Orders</c> et <c>Products</c>) et n'accède jamais à Products.db.
/// </summary>
public class OrdersAPIContext : DbContext
{
    public OrdersAPIContext(DbContextOptions<OrdersAPIContext> options)
        : base(options)
    {
    }

    public DbSet<Orders> Orders => Set<Orders>();

    /// <summary>Projection locale des produits reçus de ProductsAPI.</summary>
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Orders>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(order => order.OrdersId);
            entity.Property(order => order.OrderDate).IsRequired();
            entity.Property(order => order.CustomerName).HasMaxLength(100);
            entity.Property(order => order.ProductId).IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(product => product.Id);

            // L'identifiant vient de ProductsAPI : il ne doit jamais être généré
            // par SQLite. ValueGeneratedNever() est indispensable pour accepter
            // l'ID du message et garantir l'unicité métier.
            entity.Property(product => product.Id).ValueGeneratedNever();
            entity.Property(product => product.Name).IsRequired().HasMaxLength(100);
        });
    }
}
