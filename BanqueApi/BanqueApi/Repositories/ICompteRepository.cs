using BanqueApi.Models;

namespace BanqueApi.Repositories;

public interface ICompteRepository
{
    Task<IEnumerable<Compte>> GetAll();
    Task<Compte?> GetById(int compteId);
    Task<Compte> Add(Compte c);
    Task<int> Nombre_op(int numeroCompte);
    Task<Compte?> Update(Compte c);
    Task<Compte?> Delete(int compteId);
    Task<IEnumerable<Compte>> Search(int clientId);
    Task<Operation> Add(Operation o);
    Task<Compte?> Maj_solde(int numcompte, Operation o);
    Task<IEnumerable<Operation>> Extrait_Compte(int numcompte, DateTime d);
    Task<double> Solde_Total(int clientId);
    Task MAJVirement(int numcompte1, int numcompte2, double m);
    Task<double> Total_Retraits(int clientId);
    Task<bool> ClientExists(int clientId);
    Task<int> Nombre_op_client(int clientId);
}
