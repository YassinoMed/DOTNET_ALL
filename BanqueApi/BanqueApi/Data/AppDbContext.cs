using BanqueApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BanqueApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Compte> Comptes => Set<Compte>();
    public DbSet<Operation> Operations => Set<Operation>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Compte>().HasIndex(c => c.NumeroCompte).IsUnique();

        mb.Entity<Compte>()
          .HasOne(c => c.Client)
          .WithMany(c => c.Comptes)
          .HasForeignKey(c => c.ClientId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Operation>()
          .HasOne(o => o.Compte)
          .WithMany(c => c.Operations)
          .HasForeignKey(o => o.CompteId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Client>().HasData(
            new Client
            {
                Id = 1,
                Nom = "Ben Ali",
                Prenom = "Ahmed",
                Email = "ahmed@mail.com",
                Tel = "20123456",
                Adresse = "Sfax"
            },
            new Client
            {
                Id = 2,
                Nom = "Trabelsi",
                Prenom = "Sara",
                Email = "sara@mail.com",
                Tel = "98123456",
                Adresse = "Tunis"
            });
    }
}