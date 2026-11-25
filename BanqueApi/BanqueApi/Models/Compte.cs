using System.ComponentModel.DataAnnotations;

namespace BanqueApi.Models;

public class Compte
{
    public int Id { get; set; }
    [Required, Range(1, int.MaxValue)] public int NumeroCompte { get; set; }
    public DateTime DateCreation { get; set; } = DateTime.Now;
    [Range(0, double.MaxValue, ErrorMessage = "Le solde doit être positif")]
    public double Solde { get; set; }
    [Required] public int ClientId { get; set; }
    [Required, StringLength(30)] public string TypeCompte { get; set; } = "Courant";

    public Client? Client { get; set; }
    public ICollection<Operation>? Operations { get; set; }
}
