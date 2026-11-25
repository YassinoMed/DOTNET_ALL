using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Data
{
    /// <summary>
    /// Contexte EF Core de l'application.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.Description)
                    .HasMaxLength(500);

                entity.Property(p => p.Price)
                    .HasPrecision(18, 2);

                entity.Property(p => p.Stock)
                    .IsRequired();

                entity.HasIndex(p => p.Name);

                // Données initiales obligatoires du TP (3 produits).
                entity.HasData(
                    new Product
                    {
                        Id = 1,
                        Name = "Laptop",
                        Description = "High-end gaming laptop",
                        Price = 1500.00m,
                        Stock = 10
                    },
                    new Product
                    {
                        Id = 2,
                        Name = "Smartphone",
                        Description = "Latest model smartphone",
                        Price = 800.00m,
                        Stock = 25
                    },
                    new Product
                    {
                        Id = 3,
                        Name = "Wireless Headphones",
                        Description = "Noise cancelling headphones",
                        Price = 200.00m,
                        Stock = 40
                    });
            });
        }
    }
}
