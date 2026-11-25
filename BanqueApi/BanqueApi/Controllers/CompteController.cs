using BanqueApi.Models;
using BanqueApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BanqueApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CompteController : ControllerBase
{
    private readonly ICompteRepository _repo;

    public CompteController(ICompteRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Compte>>> GetComptes()
    {
        return Ok(await _repo.GetAll());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Compte>> GetCompte(int id)
    {
        var c = await _repo.GetById(id);
        if (c is null) return NotFound($"Compte {id} introuvable");
        return Ok(c);
    }

    [HttpPost]
    public async Task<ActionResult<Compte>> CreateCompte(Compte c)
    {
        if (!await _repo.ClientExists(c.ClientId))
            return BadRequest($"Le client {c.ClientId} n'existe pas");

        var created = await _repo.Add(c);
        return CreatedAtAction(nameof(GetCompte), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Compte>> UpdateCompte(int id, Compte c)
    {
        if (id != c.Id) return BadRequest("Id incohérent");
        if (!await _repo.ClientExists(c.ClientId))
            return BadRequest($"Le client {c.ClientId} n'existe pas");

        var updated = await _repo.Update(c);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<Compte>> DeleteCompte(int id)
    {
        var c = await _repo.GetById(id);
        if (c is null) return NotFound();

        if (await _repo.Nombre_op(c.NumeroCompte) > 0)
            return BadRequest("Suppression interdite : le compte possède des opérations");

        return Ok(await _repo.Delete(id));
    }

    [HttpGet("search/{clientId:int}")]
    public async Task<ActionResult<IEnumerable<Compte>>> Search(int clientId)
    {
        return Ok(await _repo.Search(clientId));
    }

    [HttpPost("operation")]
    public async Task<ActionResult<Operation>> CreateOperation(Operation p)
    {
        if (p.TypeOperation == "Virement")
            return BadRequest("Utilisez /api/compte/virement pour un virement");

        var compte = await _repo.GetById(p.CompteId);
        if (compte is null) return NotFound($"Compte {p.CompteId} introuvable");

        if (p.TypeOperation == "Retrait" && compte.Solde < p.Montant)
            return BadRequest("Solde insuffisant");

        p.DateOperation = DateTime.Now;
        var op = await _repo.Add(p);
        await _repo.Maj_solde(compte.NumeroCompte, op);
        return Ok(op);
    }

    [HttpGet("{num:int}/nombre-operations")]
    public async Task<ActionResult<int>> NombreOperations(int num)
    {
        return Ok(await _repo.Nombre_op(num));
    }

    [HttpGet("{num:int}/extrait")]
    public async Task<ActionResult<IEnumerable<Operation>>> Extrait(
        int num, [FromQuery] DateTime date)
    {
        return Ok(await _repo.Extrait_Compte(num, date));
    }

    [HttpGet("solde-total/{clientId:int}")]
    public async Task<ActionResult<double>> SoldeTotal(int clientId)
    {
        return Ok(await _repo.Solde_Total(clientId));
    }

    [HttpPost("virement")]
    public async Task<ActionResult> MAJVirement(int num1, int num2, double montant)
    {
        if (montant <= 0) return BadRequest("Montant invalide");
        if (num1 == num2) return BadRequest("Comptes identiques");
        try
        {
            await _repo.MAJVirement(num1, num2, montant);
            return Ok("Virement effectué");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("total-retraits/{clientId:int}")]
    public async Task<ActionResult<double>> TotalRetraits(int clientId)
    {
        return Ok(await _repo.Total_Retraits(clientId));
    }

    [HttpGet("client/{clientId:int}/nombre-operations")]
    public async Task<ActionResult<int>> NombreOperationsClient(int clientId)
    {
        return Ok(await _repo.Nombre_op_client(clientId));
    }
}
