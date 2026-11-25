using Microsoft.EntityFrameworkCore;
using ProductsAPI.Models;

namespace ProductsAPI.Data;

/// <summary>
/// Contexte EF Core de ProductsAPI. Il ne connaît que la base Products.db.
/// </summary>
public class ProductsAPIContext : DbContext
{
    public ProductsAPIContext(DbContextOptions<ProductsAPIContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Le PDF nomme ce DbSet <c>Product</c> (singulier). La mission impose la
    /// table <c>Products</c> : le DbSet est donc normalisé en <c>Products</c>.
    /// </summary>
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(product => product.ProductId);
            entity.Property(product => product.Name)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(product => product.StockQte).IsRequired();
            entity.Property(product => product.Price).IsRequired();
        });
    }
}
