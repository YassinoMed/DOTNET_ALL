using BanqueApi.Data;
using BanqueApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BanqueApi.Repositories;

public class CompteRepository : ICompteRepository
{
    private readonly AppDbContext _db;

    public CompteRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Compte>> GetAll()
    {
        return await _db.Comptes
            .Include(c => c.Client)
            .OrderBy(c => c.DateCreation)
            .ToListAsync();
    }

    public async Task<Compte?> GetById(int compteId)
    {
        return await _db.Comptes
            .Include(c => c.Client)
            .FirstOrDefaultAsync(c => c.Id == compteId);
    }

    public async Task<Compte> Add(Compte c)
    {
        c.Client = null;
        _db.Comptes.Add(c);
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<int> Nombre_op(int numeroCompte)
    {
        return await _db.Operations
            .CountAsync(o => o.Compte!.NumeroCompte == numeroCompte);
    }

    public async Task<Compte?> Update(Compte c)
    {
        var existing = await _db.Comptes.FindAsync(c.Id);
        if (existing is null) return null;

        existing.NumeroCompte = c.NumeroCompte;
        existing.TypeCompte = c.TypeCompte;
        existing.Solde = c.Solde;
        existing.ClientId = c.ClientId;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<Compte?> Delete(int compteId)
    {
        var c = await _db.Comptes.FindAsync(compteId);
        if (c is null) return null;

        _db.Comptes.Remove(c);
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<IEnumerable<Compte>> Search(int clientId)
    {
        return await _db.Comptes
            .Where(c => c.ClientId == clientId)
            .ToListAsync();
    }

    public async Task<Operation> Add(Operation o)
    {
        o.Compte = null;
        _db.Operations.Add(o);
        await _db.SaveChangesAsync();
        return o;
    }

    public async Task<Compte?> Maj_solde(int numcompte, Operation o)
    {
        var c = await _db.Comptes
            .FirstOrDefaultAsync(x => x.NumeroCompte == numcompte);
        if (c is null) return null;

        if (o.TypeOperation == "Retrait") c.Solde -= o.Montant;
        else if (o.TypeOperation == "Versement") c.Solde += o.Montant;

        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<IEnumerable<Operation>> Extrait_Compte(int numcompte, DateTime d)
    {
        return await _db.Operations
            .Where(o => o.Compte!.NumeroCompte == numcompte && o.DateOperation >= d)
            .OrderBy(o => o.DateOperation)
            .ToListAsync();
    }

    public async Task<double> Solde_Total(int clientId)
    {
        return await _db.Comptes
            .Where(c => c.ClientId == clientId)
            .SumAsync(c => c.Solde);
    }

    public async Task MAJVirement(int numcompte1, int numcompte2, double m)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var c1 = await _db.Comptes.FirstOrDefaultAsync(c => c.NumeroCompte == numcompte1);
        if (c1 is null) throw new InvalidOperationException("Compte source introuvable");

        var c2 = await _db.Comptes.FirstOrDefaultAsync(c => c.NumeroCompte == numcompte2);
        if (c2 is null) throw new InvalidOperationException("Compte destinataire introuvable");

        if (c1.Solde < m) throw new InvalidOperationException("Solde insuffisant");

        c1.Solde -= m;
        c2.Solde += m;

        _db.Operations.Add(new Operation
        {
            CompteId = c1.Id,
            TypeOperation = "Virement",
            Montant = m,
            DateOperation = DateTime.Now
        });
        _db.Operations.Add(new Operation
        {
            CompteId = c2.Id,
            TypeOperation = "Virement",
            Montant = m,
            DateOperation = DateTime.Now
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<double> Total_Retraits(int clientId)
    {
        return await _db.Operations
            .Where(o => o.Compte!.ClientId == clientId && o.TypeOperation == "Retrait")
            .SumAsync(o => o.Montant);
    }

    public async Task<bool> ClientExists(int clientId)
    {
        return await _db.Clients.AnyAsync(c => c.Id == clientId);
    }

    public async Task<int> Nombre_op_client(int clientId)
    {
        return await _db.Operations.CountAsync(o => o.Compte!.ClientId == clientId);
    }
}
